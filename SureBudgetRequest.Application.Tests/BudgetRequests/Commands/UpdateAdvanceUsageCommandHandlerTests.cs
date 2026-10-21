using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.UpdateAdvanceUsage;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class UpdateAdvanceUsageCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly UpdateAdvanceUsageCommandHandler _handler;

    public UpdateAdvanceUsageCommandHandlerTests()
    {
        _handler = new UpdateAdvanceUsageCommandHandler(_budgetRequestRepository, _unitOfWork);
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
        request.ApproveBy(Guid.NewGuid(), coaId: Guid.NewGuid(), reconciliationDeadline: DateTime.UtcNow.AddDays(14));
        request.RecordPayment(amount, DateTime.UtcNow, Guid.NewGuid(), "ADV-PAY", "Disbursed");

        return request;
    }

    [Fact]
    public async Task Handle_WhenBudgetRequestNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        _budgetRequestRepository.GetByIdAsync(reqId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var command = new UpdateAdvanceUsageCommand(reqId, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, 100_000m, "Lunch");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.NotFound(reqId));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotRequester_ReturnsOnlyRequesterError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var command = new UpdateAdvanceUsageCommand(request.Id, Guid.NewGuid(), otherUserId, DateTime.UtcNow, 100_000m, "Lunch");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyRequesterCanUpdateUsage);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUsageNotFoundOnRequest_ReturnsDomainFailureWithoutSaving()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var missingUsageId = Guid.NewGuid();
        var command = new UpdateAdvanceUsageCommand(request.Id, missingUsageId, requesterId, DateTime.UtcNow, 100_000m, "Lunch");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Usage line item not found on this request");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task Handle_WhenAmountIsZeroOrNegative_ReturnsDomainFailureWithoutSaving(decimal invalidAmount)
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId);
        request.AddAdvanceUsage(DateTime.UtcNow, 100_000m, "Initial expense", null, requesterId, DateTime.UtcNow);

        var usage = request.AdvanceUsages.First();
        var usageId = Guid.NewGuid();
        typeof(AdvanceUsage).GetProperty(nameof(AdvanceUsage.Id))!.SetValue(usage, usageId);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var command = new UpdateAdvanceUsageCommand(request.Id, usageId, requesterId, DateTime.UtcNow, invalidAmount, "Updated");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Usage amount must be greater than zero");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValid_UpdatesUsageSavesChangesAndReturnsSuccess()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId);
        request.AddAdvanceUsage(DateTime.UtcNow, 100_000m, "Original expense", null, requesterId, DateTime.UtcNow);

        var usage = request.AdvanceUsages.First();
        var usageId = Guid.NewGuid();
        typeof(AdvanceUsage).GetProperty(nameof(AdvanceUsage.Id))!.SetValue(usage, usageId);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var command = new UpdateAdvanceUsageCommand(
            request.Id,
            usageId,
            requesterId,
            DateTime.UtcNow,
            180_000m,
            "Updated flight expense");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        usage.Amount.Should().Be(180_000m);
        usage.Description.Should().Be("Updated flight expense");
        request.TotalUsageRecorded.Should().Be(180_000m);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreatePendingReconciliationAdvance(requesterId);
        request.AddAdvanceUsage(DateTime.UtcNow, 100_000m, "Original expense", null, requesterId, DateTime.UtcNow);

        var usage = request.AdvanceUsages.First();
        var usageId = Guid.NewGuid();
        typeof(AdvanceUsage).GetProperty(nameof(AdvanceUsage.Id))!.SetValue(usage, usageId);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _budgetRequestRepository.GetByIdAsync(request.Id, ct).Returns(request);

        var command = new UpdateAdvanceUsageCommand(
            request.Id,
            usageId,
            requesterId,
            DateTime.UtcNow,
            120_000m,
            "Updated Taxi");

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _budgetRequestRepository.Received(1).GetByIdAsync(request.Id, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
