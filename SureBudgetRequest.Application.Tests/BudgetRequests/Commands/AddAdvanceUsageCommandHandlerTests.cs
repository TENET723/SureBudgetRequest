using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.AddAdvanceUsage;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class AddAdvanceUsageCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AddAdvanceUsageCommandHandler _handler;

    public AddAdvanceUsageCommandHandlerTests()
    {
        _handler = new AddAdvanceUsageCommandHandler(_budgetRequestRepository, _unitOfWork);
    }

    private static BudgetRequest CreatePendingReconciliationAdvance(Guid requesterId, decimal amount = 500_000m)
    {
        var headId = Guid.NewGuid();
        var request = BudgetRequest.CreateDraft(
            requesterId, "Alice", BudgetRequestType.Advance, headId, "Bob Head",
            DateTime.UtcNow.AddHours(6.5).Date.AddDays(2),
            amount, "MMK", "Advance Trip", "Alice", "Job", Guid.NewGuid(),
            false, null);

        request.Submit(
            Guid.NewGuid(), 1_000_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, headId, "Bob Head", "Alice", false, 1);

        request.ApproveBy(headId);

        var deadline = DateTime.UtcNow.AddDays(14);
        request.ApproveBy(Guid.NewGuid(), coaId: Guid.NewGuid(), reconciliationDeadline: deadline);

        // Record full disbursement
        request.RecordPayment(amount, DateTime.UtcNow, Guid.NewGuid(), "ADV-PAY", "Disbursed");

        return request;
    }

    [Fact]
    public async Task Handle_WhenBudgetRequestNotFound_ReturnsFailure()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        _budgetRequestRepository.GetByIdAsync(requestId, Arg.Any<CancellationToken>())
            .Returns((BudgetRequest?)null);

        var command = new AddAdvanceUsageCommand(
            requestId,
            Guid.NewGuid(),
            DateTime.UtcNow,
            100_000m,
            "Meals");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Budget request not found.");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserIsNotRequester_ReturnsForbiddenFailure()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId, 500_000m);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>())
            .Returns(request);

        var command = new AddAdvanceUsageCommand(
            request.Id,
            otherUserId,
            DateTime.UtcNow,
            100_000m,
            "Meals");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Only the requester can record usage on their own advance.");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task Handle_WhenAmountIsZeroOrNegative_ReturnsFailureWithoutSavingChanges(decimal invalidAmount)
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId, 100_000m);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>())
            .Returns(request);

        var command = new AddAdvanceUsageCommand(
            request.Id,
            requesterId,
            DateTime.UtcNow,
            invalidAmount,
            "Invalid expense");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Usage amount must be greater than zero.");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenDescriptionIsNullOrWhiteSpace_ReturnsFailureWithoutSavingChanges(string invalidDescription)
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId, 100_000m);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>())
            .Returns(request);

        var command = new AddAdvanceUsageCommand(
            request.Id,
            requesterId,
            DateTime.UtcNow,
            50_000m,
            invalidDescription);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("A description is required for each usage line item.");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSpentOnBeforeApprovalDate_ReturnsFailureWithoutSavingChanges()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId, 100_000m);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>())
            .Returns(request);

        var command = new AddAdvanceUsageCommand(
            request.Id,
            requesterId,
            DateTime.UtcNow.AddDays(-30),
            50_000m,
            "Old Expense");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Expense date cannot be before the request's approval date.");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValid_AddsUsageSavesChangesAndReturnsSuccess()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId, 500_000m);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>())
            .Returns(request);

        var command = new AddAdvanceUsageCommand(
            request.Id,
            requesterId,
            DateTime.UtcNow,
            150_000m,
            "Conference Fee");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.AdvanceUsages.Should().ContainSingle();
        request.TotalUsageRecorded.Should().Be(150_000m);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId, 500_000m);
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _budgetRequestRepository.GetByIdAsync(request.Id, ct).Returns(request);

        var command = new AddAdvanceUsageCommand(
            request.Id,
            requesterId,
            DateTime.UtcNow,
            50_000m,
            "Taxi");

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _budgetRequestRepository.Received(1).GetByIdAsync(request.Id, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
