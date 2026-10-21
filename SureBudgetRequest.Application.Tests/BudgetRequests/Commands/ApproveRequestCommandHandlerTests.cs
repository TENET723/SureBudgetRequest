using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.ApproveRequest;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class ApproveRequestCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICoaRepository _coaRepository = Substitute.For<ICoaRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();
    private readonly ApproveRequestCommandHandler _handler;

    public ApproveRequestCommandHandlerTests()
    {
        _handler = new ApproveRequestCommandHandler(
            _budgetRequestRepository,
            _userRepository,
            _coaRepository,
            _unitOfWork,
            _dispatcher);
    }

    private static BudgetRequest CreateSubmittedRequest(
        Guid requesterId,
        Guid deptHeadId,
        BudgetRequestType type = BudgetRequestType.Standard,
        decimal amount = 100_000m)
    {
        var request = BudgetRequest.CreateDraft(
            requesterId, "Alice", type, deptHeadId, "Bob Head",
            DateTime.UtcNow.AddHours(6.5).Date.AddDays(2),
            amount, "MMK", "Test title", "Alice", "Job", Guid.NewGuid(),
            false, null);

        request.Submit(
            Guid.NewGuid(), 1_000_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, deptHeadId, "Bob Head", "Alice", false, 1);

        return request;
    }

    [Fact]
    public async Task Handle_WhenBudgetRequestNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        _budgetRequestRepository.GetByIdAsync(reqId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var command = new ApproveRequestCommand(reqId, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.NotFound(reqId));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenApproverNotFound_ReturnsUserNotFoundError()
    {
        // Arrange
        var approverId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), Guid.NewGuid());

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(approverId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new ApproveRequestCommand(request.Id, approverId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(approverId));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Management)]
    [InlineData(UserRole.Finance)]
    [InlineData(UserRole.Admin)]
    public async Task Handle_WhenPendingDeptHeadAndApproverNotDeptHead_ReturnsRoleUnauthorized(UserRole nonDeptHeadRole)
    {
        // Arrange
        var approverId = Guid.NewGuid();
        var approver = new User("approver", "Approver Name", "app@sure.com", Guid.NewGuid(), nonDeptHeadRole);
        var request = CreateSubmittedRequest(Guid.NewGuid(), approverId);

        request.Status.Should().Be(RequestStatus.PendingDeptHead);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(approverId, Arg.Any<CancellationToken>()).Returns(approver);

        var command = new ApproveRequestCommand(request.Id, approverId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.RoleUnauthorized");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPendingFinanceAndUserIsNotFinanceApprover_ReturnsRestrictedForbiddenError()
    {
        // Arrange
        var deptHeadId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), deptHeadId);
        request.ApproveBy(deptHeadId); // Moves to PendingFinance

        var financeUserId = Guid.NewGuid();
        var financeUser = new User("finpayer", "Fin Payer", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);
        financeUser.IsFinanceApprover.Should().BeFalse(); // Not approver

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns(financeUser);

        var command = new ApproveRequestCommand(request.Id, financeUserId, CoaId: Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BudgetRequest.FinanceUserRestricted");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)] // null
    [InlineData(true)]  // Guid.Empty
    public async Task Handle_WhenPendingFinanceAndCoaIdMissingOrEmpty_ReturnsCoaRequiredError(bool useEmptyGuid)
    {
        // Arrange
        var deptHeadId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), deptHeadId);
        request.ApproveBy(deptHeadId); // PendingFinance

        var financeApproverId = Guid.NewGuid();
        var financeApprover = new User("finapp", "Fin Approver", "finapp@sure.com", Guid.NewGuid(), UserRole.Finance);
        financeApprover.SetFinanceApprover(true);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeApproverId, Arg.Any<CancellationToken>()).Returns(financeApprover);

        var command = new ApproveRequestCommand(
            request.Id,
            financeApproverId,
            CoaId: useEmptyGuid ? Guid.Empty : null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.CoaRequiredForFinanceApproval);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPendingFinanceAndCoaNotFound_ReturnsCoaNotFoundError()
    {
        // Arrange
        var deptHeadId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), deptHeadId);
        request.ApproveBy(deptHeadId);

        var financeApproverId = Guid.NewGuid();
        var financeApprover = new User("finapp", "Fin Approver", "finapp@sure.com", Guid.NewGuid(), UserRole.Finance);
        financeApprover.SetFinanceApprover(true);

        var missingCoaId = Guid.NewGuid();
        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeApproverId, Arg.Any<CancellationToken>()).Returns(financeApprover);
        _coaRepository.GetByIdAsync(missingCoaId, Arg.Any<CancellationToken>()).Returns((Coa?)null);

        var command = new ApproveRequestCommand(request.Id, financeApproverId, CoaId: missingCoaId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.CoaNotFound);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPendingFinanceAndCoaIsDeactivated_ReturnsCoaDeactivatedError()
    {
        // Arrange
        var deptHeadId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), deptHeadId);
        request.ApproveBy(deptHeadId);

        var financeApproverId = Guid.NewGuid();
        var financeApprover = new User("finapp", "Fin Approver", "finapp@sure.com", Guid.NewGuid(), UserRole.Finance);
        financeApprover.SetFinanceApprover(true);

        var coaId = Guid.NewGuid();
        var inactiveCoa = new Coa("COA-101", "Office Supplies");
        typeof(Coa).GetProperty(nameof(Coa.Id))!.SetValue(inactiveCoa, coaId);
        inactiveCoa.Deactivate();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeApproverId, Arg.Any<CancellationToken>()).Returns(financeApprover);
        _coaRepository.GetByIdAsync(coaId, Arg.Any<CancellationToken>()).Returns(inactiveCoa);

        var command = new ApproveRequestCommand(request.Id, financeApproverId, CoaId: coaId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.CoaDeactivated(inactiveCoa.Code));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDeptHeadApproves_DispatchesNotificationSavesAndReturnsSuccess()
    {
        // Arrange
        var deptHeadId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), deptHeadId);

        var deptHead = new User("depthead", "Dept Head Name", "head@sure.com", Guid.NewGuid(), UserRole.DepartmentHead);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(deptHeadId, Arg.Any<CancellationToken>()).Returns(deptHead);

        var command = new ApproveRequestCommand(request.Id, deptHeadId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PendingFinance);

        await _dispatcher.Received(1).DispatchAsync(
            request,
            RequestStatus.PendingDeptHead,
            deptHeadId,
            "Dept Head Name",
            null,
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFinanceApprovesAdvance_NormalizesDeadlineToUtcDispatchesAndSaves()
    {
        // Arrange
        var deptHeadId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), deptHeadId, BudgetRequestType.Advance, 200_000m);
        request.ApproveBy(deptHeadId);

        var financeApproverId = Guid.NewGuid();
        var financeApprover = new User("finapp", "Finance Head", "finapp@sure.com", Guid.NewGuid(), UserRole.Finance);
        financeApprover.SetFinanceApprover(true);

        var coaId = Guid.NewGuid();
        var activeCoa = new Coa("COA-200", "Travel Advances");
        typeof(Coa).GetProperty(nameof(Coa.Id))!.SetValue(activeCoa, coaId);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeApproverId, Arg.Any<CancellationToken>()).Returns(financeApprover);
        _coaRepository.GetByIdAsync(coaId, Arg.Any<CancellationToken>()).Returns(activeCoa);

        var deadline = DateTime.Now.AddDays(14); // Local or Unspecified kind
        var command = new ApproveRequestCommand(
            request.Id,
            financeApproverId,
            coaId,
            deadline);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.Approved);
        request.ReconciliationDeadline.Should().NotBeNull();
        request.ReconciliationDeadline!.Value.Kind.Should().Be(DateTimeKind.Utc);

        await _dispatcher.Received(1).DispatchAsync(
            request,
            RequestStatus.PendingFinance,
            financeApproverId,
            "Finance Head",
            null,
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDomainApprovalFails_ReturnsFailureWithoutDispatchingOrSaving()
    {
        // Arrange: Advance request missing reconciliation deadline fails in Domain!
        var deptHeadId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), deptHeadId, BudgetRequestType.Advance, 200_000m);
        request.ApproveBy(deptHeadId);

        var financeApproverId = Guid.NewGuid();
        var financeApprover = new User("finapp", "Finance Head", "finapp@sure.com", Guid.NewGuid(), UserRole.Finance);
        financeApprover.SetFinanceApprover(true);

        var coaId = Guid.NewGuid();
        var activeCoa = new Coa("COA-200", "Travel Advances");
        typeof(Coa).GetProperty(nameof(Coa.Id))!.SetValue(activeCoa, coaId);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeApproverId, Arg.Any<CancellationToken>()).Returns(financeApprover);
        _coaRepository.GetByIdAsync(coaId, Arg.Any<CancellationToken>()).Returns(activeCoa);

        var command = new ApproveRequestCommand(
            request.Id,
            financeApproverId,
            coaId,
            ReconciliationDeadline: null); // Advance requires deadline, so domain fails!

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Reconciliation deadline is required");

        await _dispatcher.DidNotReceive().DispatchAsync(
            Arg.Any<BudgetRequest>(),
            Arg.Any<RequestStatus>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var deptHeadId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), deptHeadId);
        var deptHead = new User("depthead", "Dept Head Name", "head@sure.com", Guid.NewGuid(), UserRole.DepartmentHead);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _budgetRequestRepository.GetByIdAsync(request.Id, ct).Returns(request);
        _userRepository.GetByIdAsync(deptHeadId, ct).Returns(deptHead);

        var command = new ApproveRequestCommand(request.Id, deptHeadId);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _budgetRequestRepository.Received(1).GetByIdAsync(request.Id, ct);
        await _userRepository.Received(1).GetByIdAsync(deptHeadId, ct);
        await _dispatcher.Received(1).DispatchAsync(request, RequestStatus.PendingDeptHead, deptHeadId, "Dept Head Name", null, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
