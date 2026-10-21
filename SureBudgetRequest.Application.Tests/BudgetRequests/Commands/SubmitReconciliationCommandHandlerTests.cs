using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.SubmitReconciliation;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class SubmitReconciliationCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();
    private readonly SubmitReconciliationCommandHandler _handler;

    public SubmitReconciliationCommandHandlerTests()
    {
        _handler = new SubmitReconciliationCommandHandler(
            _budgetRequestRepository,
            _userRepository,
            _unitOfWork,
            _dispatcher);
    }

    private static BudgetRequest CreatePendingReconciliationAdvance(Guid requesterId, decimal advanceAmount = 500_000m)
    {
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

        return request;
    }

    [Fact]
    public async Task Handle_WhenBudgetRequestNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        _budgetRequestRepository.GetByIdAsync(reqId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var command = new SubmitReconciliationCommand(reqId, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.NotFound(reqId));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserIsNotRequester_ReturnsOnlyRequesterError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var command = new SubmitReconciliationCommand(request.Id, otherUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyRequesterCanSubmitReconciliation);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoUsageItemsRecorded_ReturnsDomainFailureWithoutDispatchingOrSaving()
    {
        // Arrange: Zero usages recorded
        var requesterId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var command = new SubmitReconciliationCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("At least one usage line item is required before submitting reconciliation");

        await _dispatcher.DidNotReceive().DispatchReconciliationSubmittedAsync(
            Arg.Any<BudgetRequest>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUsageEqualsAdvance_ReconcilesDispatchesNotificationAndSaves()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId, 500_000m);
        request.AddAdvanceUsage(DateTime.UtcNow, 500_000m, "Exact match", null, requesterId, DateTime.UtcNow);

        var user = new User("alice", "Alice Requester", "alice@sure.com", Guid.NewGuid(), UserRole.Employee);
        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new SubmitReconciliationCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.Reconciled);

        await _dispatcher.Received(1).DispatchReconciliationSubmittedAsync(
            request,
            "Alice Requester",
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUsageLessThanAdvance_MovesToAwaitingRefundDispatchesNotificationAndSaves()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId, 500_000m);
        request.AddAdvanceUsage(DateTime.UtcNow, 300_000m, "Under-spent", null, requesterId, DateTime.UtcNow);

        var user = new User("alice", "Alice Requester", "alice@sure.com", Guid.NewGuid(), UserRole.Employee);
        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new SubmitReconciliationCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.AwaitingRefund);
        request.RefundAmount.Should().Be(200_000m);

        await _dispatcher.Received(1).DispatchReconciliationSubmittedAsync(
            request,
            "Alice Requester",
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUsageGreaterThanAdvance_MovesToAwaitingReimbursementDispatchesNotificationAndSaves()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId, 500_000m);
        request.AddAdvanceUsage(DateTime.UtcNow, 750_000m, "Over-spent", null, requesterId, DateTime.UtcNow);

        var user = new User("alice", "Alice Requester", "alice@sure.com", Guid.NewGuid(), UserRole.Employee);
        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new SubmitReconciliationCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.AwaitingReimbursement);
        request.ReimbursementAmount.Should().Be(250_000m);

        await _dispatcher.Received(1).DispatchReconciliationSubmittedAsync(
            request,
            "Alice Requester",
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId, 500_000m);
        request.AddAdvanceUsage(DateTime.UtcNow, 500_000m, "Exact", null, requesterId, DateTime.UtcNow);

        var user = new User("alice", "Alice", "alice@sure.com", Guid.NewGuid(), UserRole.Employee);
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _budgetRequestRepository.GetByIdAsync(request.Id, ct).Returns(request);
        _userRepository.GetByIdAsync(requesterId, ct).Returns(user);

        var command = new SubmitReconciliationCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _budgetRequestRepository.Received(1).GetByIdAsync(request.Id, ct);
        await _userRepository.Received(1).GetByIdAsync(requesterId, ct);
        await _dispatcher.Received(1).DispatchReconciliationSubmittedAsync(request, "Alice", ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
