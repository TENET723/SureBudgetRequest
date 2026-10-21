using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.RecordRefund;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class RecordRefundCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();
    private readonly RecordRefundCommandHandler _handler;

    public RecordRefundCommandHandlerTests()
    {
        _handler = new RecordRefundCommandHandler(
            _budgetRequestRepository,
            _userRepository,
            _unitOfWork,
            _dispatcher);
    }

    private static BudgetRequest CreateAwaitingRefundAdvance(decimal advanceAmount = 500_000m, decimal usageAmount = 350_000m)
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

        // Record partial usage
        request.AddAdvanceUsage(DateTime.UtcNow, usageAmount, "Expenses", null, requesterId, DateTime.UtcNow);

        // Submit reconciliation -> moves to AwaitingRefund (refund = advanceAmount - usageAmount = 150_000m)
        request.SubmitReconciliation(requesterId, DateTime.UtcNow.AddMinutes(-2));

        return request;
    }

    [Fact]
    public async Task Handle_WhenBudgetRequestNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        _budgetRequestRepository.GetByIdAsync(reqId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var command = new RecordRefundCommand(reqId, Guid.NewGuid(), 150_000m, DateTime.UtcNow);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.NotFound(reqId));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFinanceUserNotFound_ReturnsOnlyFinanceCanRecordRefundError()
    {
        // Arrange
        var request = CreateAwaitingRefundAdvance();
        var financeUserId = Guid.NewGuid();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new RecordRefundCommand(request.Id, financeUserId, 150_000m, DateTime.UtcNow);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyFinanceCanRecordRefund);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.DepartmentHead)]
    [InlineData(UserRole.Management)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Accounting)]
    [InlineData(UserRole.Support)]
    public async Task Handle_WhenUserIsNotFinanceRole_ReturnsOnlyFinanceCanRecordRefundError(UserRole nonFinanceRole)
    {
        // Arrange
        var request = CreateAwaitingRefundAdvance();
        var userId = Guid.NewGuid();
        var user = new User("user", "User", "user@sure.com", Guid.NewGuid(), nonFinanceRole);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new RecordRefundCommand(request.Id, userId, 150_000m, DateTime.UtcNow);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyFinanceCanRecordRefund);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAmountDoesNotMatchRefundAmount_ReturnsDomainFailureWithoutDispatchingOrSaving()
    {
        // Arrange
        var request = CreateAwaitingRefundAdvance(500_000m, 350_000m); // refund is 150_000m
        var financeUserId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns(financeUser);

        var command = new RecordRefundCommand(request.Id, financeUserId, 100_000m, DateTime.UtcNow); // Mismatch!

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("The refund amount must exactly match the outstanding refund");

        await _dispatcher.DidNotReceive().DispatchRefundRecordedAsync(
            Arg.Any<BudgetRequest>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValid_RecordsRefundDispatchesNotificationAndSaves()
    {
        // Arrange
        var request = CreateAwaitingRefundAdvance(500_000m, 350_000m); // refund is 150_000m
        var financeUserId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance Cashier", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns(financeUser);

        var receivedAt = DateTime.UtcNow; // UTC time after submission
        var command = new RecordRefundCommand(request.Id, financeUserId, 150_000m, receivedAt);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.Reconciled);

        await _dispatcher.Received(1).DispatchRefundRecordedAsync(
            request,
            "Finance Cashier",
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var request = CreateAwaitingRefundAdvance(500_000m, 350_000m);
        var financeUserId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance Cashier", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _budgetRequestRepository.GetByIdAsync(request.Id, ct).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, ct).Returns(financeUser);

        var command = new RecordRefundCommand(request.Id, financeUserId, 150_000m, DateTime.UtcNow);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _budgetRequestRepository.Received(1).GetByIdAsync(request.Id, ct);
        await _userRepository.Received(1).GetByIdAsync(financeUserId, ct);
        await _dispatcher.Received(1).DispatchRefundRecordedAsync(request, "Finance Cashier", ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
