using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.RecordReimbursement;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class RecordReimbursementCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();
    private readonly RecordReimbursementCommandHandler _handler;

    public RecordReimbursementCommandHandlerTests()
    {
        _handler = new RecordReimbursementCommandHandler(
            _budgetRequestRepository,
            _userRepository,
            _unitOfWork,
            _dispatcher);
    }

    private static BudgetRequest CreateAwaitingReimbursementAdvance(decimal advanceAmount = 500_000m, decimal usageAmount = 650_000m)
    {
        var requesterId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var request = BudgetRequest.CreateDraft(
            requesterId, "Alice", BudgetRequestType.Advance, headId, "Bob Head",
            DateTime.UtcNow.AddHours(6.5).Date.AddDays(2),
            advanceAmount, "MMK", "Advance Trip", "Alice", "Job", Guid.NewGuid(),
            false, null);

        request.Submit(
            Guid.NewGuid(), 1_000_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, headId, "Bob Head", "Alice", false, 1);

        request.ApproveBy(headId);
        request.ApproveBy(Guid.NewGuid(), coaId: Guid.NewGuid(), reconciliationDeadline: DateTime.UtcNow.AddDays(14));
        request.RecordPayment(advanceAmount, DateTime.UtcNow, Guid.NewGuid(), "ADV-PAY", "Disbursed");

        // Record usage exceeding advance
        request.AddAdvanceUsage(DateTime.UtcNow, usageAmount, "Over-expenses", null, requesterId, DateTime.UtcNow);

        // Submit reconciliation -> moves to AwaitingReimbursement (reimbursement = usageAmount - advanceAmount = 150_000m)
        request.SubmitReconciliation(requesterId, DateTime.UtcNow.AddMinutes(-2));

        return request;
    }

    [Fact]
    public async Task Handle_WhenBudgetRequestNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        _budgetRequestRepository.GetByIdAsync(reqId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var command = new RecordReimbursementCommand(reqId, Guid.NewGuid(), 150_000m, DateTime.UtcNow);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.NotFound(reqId));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFinanceUserNotFound_ReturnsOnlyFinanceCanRecordReimbursementError()
    {
        // Arrange
        var request = CreateAwaitingReimbursementAdvance();
        var financeUserId = Guid.NewGuid();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new RecordReimbursementCommand(request.Id, financeUserId, 150_000m, DateTime.UtcNow);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyFinanceCanRecordReimbursement);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.DepartmentHead)]
    [InlineData(UserRole.Management)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Accounting)]
    [InlineData(UserRole.Support)]
    public async Task Handle_WhenUserIsNotFinanceRole_ReturnsOnlyFinanceCanRecordReimbursementError(UserRole nonFinanceRole)
    {
        // Arrange
        var request = CreateAwaitingReimbursementAdvance();
        var userId = Guid.NewGuid();
        var user = new User("user", "User", "user@sure.com", Guid.NewGuid(), nonFinanceRole);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new RecordReimbursementCommand(request.Id, userId, 150_000m, DateTime.UtcNow);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyFinanceCanRecordReimbursement);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAmountDoesNotMatchReimbursementAmount_ReturnsDomainFailureWithoutDispatchingOrSaving()
    {
        // Arrange
        var request = CreateAwaitingReimbursementAdvance(500_000m, 650_000m); // reimbursement is 150_000m
        var financeUserId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns(financeUser);

        var command = new RecordReimbursementCommand(request.Id, financeUserId, 200_000m, DateTime.UtcNow); // Mismatch!

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("The reimbursement amount must exactly match the outstanding reimbursement");

        await _dispatcher.DidNotReceive().DispatchReimbursementRecordedAsync(
            Arg.Any<BudgetRequest>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValid_RecordsReimbursementDispatchesNotificationAndSaves()
    {
        // Arrange
        var request = CreateAwaitingReimbursementAdvance(500_000m, 650_000m); // reimbursement is 150_000m
        var financeUserId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance Payer", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns(financeUser);

        var paidAt = DateTime.UtcNow;
        var command = new RecordReimbursementCommand(request.Id, financeUserId, 150_000m, paidAt);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.Reconciled);

        await _dispatcher.Received(1).DispatchReimbursementRecordedAsync(
            request,
            "Finance Payer",
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var request = CreateAwaitingReimbursementAdvance(500_000m, 650_000m);
        var financeUserId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance Payer", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _budgetRequestRepository.GetByIdAsync(request.Id, ct).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, ct).Returns(financeUser);

        var command = new RecordReimbursementCommand(request.Id, financeUserId, 150_000m, DateTime.UtcNow);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _budgetRequestRepository.Received(1).GetByIdAsync(request.Id, ct);
        await _userRepository.Received(1).GetByIdAsync(financeUserId, ct);
        await _dispatcher.Received(1).DispatchReimbursementRecordedAsync(request, "Finance Payer", ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
