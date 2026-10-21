using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.SendBackRequest;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class SendBackRequestCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();
    private readonly SendBackRequestCommandHandler _handler;

    public SendBackRequestCommandHandlerTests()
    {
        _handler = new SendBackRequestCommandHandler(
            _budgetRequestRepository,
            _userRepository,
            _unitOfWork,
            _dispatcher);
    }

    private static BudgetRequest CreateSubmittedRequest(Guid requesterId, Guid deptHeadId)
    {
        var request = BudgetRequest.CreateDraft(
            requesterId, "Alice", BudgetRequestType.Standard, deptHeadId, "Bob Head",
            DateTime.UtcNow.AddHours(6.5).Date.AddDays(2),
            100_000m, "MMK", "Test title", "Alice", "Job", Guid.NewGuid(),
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

        var command = new SendBackRequestCommand(reqId, Guid.NewGuid(), "Please fix");

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
        var request = CreateSubmittedRequest(Guid.NewGuid(), Guid.NewGuid());
        var approverId = Guid.NewGuid();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(approverId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new SendBackRequestCommand(request.Id, approverId, "Please fix");

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
    public async Task Handle_WhenApproverRoleUnauthorized_ReturnsRoleUnauthorizedError(UserRole nonDeptHeadRole)
    {
        // Arrange: Request is at PendingDeptHead
        var approverId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), approverId);
        var approver = new User("user", "User", "user@sure.com", Guid.NewGuid(), nonDeptHeadRole);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(approverId, Arg.Any<CancellationToken>()).Returns(approver);

        var command = new SendBackRequestCommand(request.Id, approverId, "Please fix");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.RoleUnauthorized");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPendingFinanceAndUserIsNotFinanceApprover_ReturnsFinanceApproverRequiredError()
    {
        // Arrange
        var deptHeadId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), deptHeadId);
        request.ApproveBy(deptHeadId); // Moves to PendingFinance

        var financeUserId = Guid.NewGuid();
        var financeUser = new User("finpayer", "Fin Payer", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);
        financeUser.IsFinanceApprover.Should().BeFalse();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns(financeUser);

        var command = new SendBackRequestCommand(request.Id, financeUserId, "Finance needs more detail");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.FinanceApproverRequired);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenApprovedAndFinanceUserIsNotFinanceApprover_ReturnsFinanceApproverRequiredError()
    {
        // Arrange
        var deptHeadId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), deptHeadId);
        request.ApproveBy(deptHeadId);
        request.ApproveBy(Guid.NewGuid(), coaId: Guid.NewGuid()); // Approved

        var financeUserId = Guid.NewGuid();
        var financeUser = new User("finpayer", "Fin Payer", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);
        financeUser.IsFinanceApprover.Should().BeFalse();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns(financeUser);

        var command = new SendBackRequestCommand(request.Id, financeUserId, "Revoking approval");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.FinanceApproverRequired);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenApprovedAndAdminSendsBack_SendsBackSuccessfully()
    {
        // Arrange
        var deptHeadId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), deptHeadId);
        request.ApproveBy(deptHeadId);
        request.ApproveBy(Guid.NewGuid(), coaId: Guid.NewGuid()); // Approved

        var adminId = Guid.NewGuid();
        var admin = new User("admin", "Admin User", "admin@sure.com", Guid.NewGuid(), UserRole.Admin);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(adminId, Arg.Any<CancellationToken>()).Returns(admin);

        var command = new SendBackRequestCommand(request.Id, adminId, "Admin intervention");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.SentBack);

        await _dispatcher.Received(1).DispatchAsync(
            request,
            RequestStatus.Approved,
            adminId,
            "Admin User",
            "Admin intervention",
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenCommentIsNullOrWhiteSpace_ReturnsDomainFailureWithoutDispatchingOrSaving(string invalidComment)
    {
        // Arrange
        var deptHeadId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), deptHeadId);
        var deptHead = new User("depthead", "Dept Head", "head@sure.com", Guid.NewGuid(), UserRole.DepartmentHead);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(deptHeadId, Arg.Any<CancellationToken>()).Returns(deptHead);

        var command = new SendBackRequestCommand(request.Id, deptHeadId, invalidComment);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("A comment is required when sending back.");

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
    public async Task Handle_WhenValidPendingDeptHead_SendsBackDispatchesNotificationAndSaves()
    {
        // Arrange
        var deptHeadId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), deptHeadId);
        var deptHead = new User("depthead", "Dept Head", "head@sure.com", Guid.NewGuid(), UserRole.DepartmentHead);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(deptHeadId, Arg.Any<CancellationToken>()).Returns(deptHead);

        var command = new SendBackRequestCommand(request.Id, deptHeadId, "Please attach invoice");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.SentBack);

        await _dispatcher.Received(1).DispatchAsync(
            request,
            RequestStatus.PendingDeptHead,
            deptHeadId,
            "Dept Head",
            "Please attach invoice",
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var deptHeadId = Guid.NewGuid();
        var request = CreateSubmittedRequest(Guid.NewGuid(), deptHeadId);
        var deptHead = new User("depthead", "Dept Head", "head@sure.com", Guid.NewGuid(), UserRole.DepartmentHead);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _budgetRequestRepository.GetByIdAsync(request.Id, ct).Returns(request);
        _userRepository.GetByIdAsync(deptHeadId, ct).Returns(deptHead);

        var command = new SendBackRequestCommand(request.Id, deptHeadId, "Please attach invoice");

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _budgetRequestRepository.Received(1).GetByIdAsync(request.Id, ct);
        await _userRepository.Received(1).GetByIdAsync(deptHeadId, ct);
        await _dispatcher.Received(1).DispatchAsync(request, RequestStatus.PendingDeptHead, deptHeadId, "Dept Head", "Please attach invoice", ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
