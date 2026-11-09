using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class BudgetRequestReconciliationTests
{
    private static BudgetRequest CreatePendingReconciliationAdvance(decimal amount = 500_000m)
    {
        var reqId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var request = BudgetRequest.CreateDraft(
            reqId, "Alice", BudgetRequestType.Advance, headId, "Bob Head",
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

    // =========================================================================
    // 1. AddAdvanceUsage Tests
    // =========================================================================

    [Fact]
    public void AddAdvanceUsage_ValidParameters_ShouldRecordUsageAndCalculateTotal()
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);
        var userId = request.RequesterId;
        var now = DateTime.UtcNow;

        // Act
        var result = request.AddAdvanceUsage(
            now, 150_000m, "Hotel accommodation", null, userId, now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.AdvanceUsages.Should().ContainSingle();
        request.TotalUsageRecorded.Should().Be(150_000m);
    }

    [Fact]
    public void AddAdvanceUsage_WhenExceedingApprovedAmount_ShouldFail()
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);
        var now = DateTime.UtcNow;

        // Act: Attempting to record 600k against 500k approved
        var result = request.AddAdvanceUsage(
            now, 600_000m, "Too expensive", null, request.RequesterId, now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Total usage cannot exceed the approved advance amount");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100_000)]
    public void AddAdvanceUsage_InvalidAmount_ShouldFail(decimal invalidAmount)
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);

        // Act
        var result = request.AddAdvanceUsage(
            DateTime.UtcNow, invalidAmount, "Item", null, request.RequesterId, DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Usage amount must be greater than zero.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddAdvanceUsage_InvalidDescription_ShouldFail(string? invalidDesc)
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);

        // Act
        var result = request.AddAdvanceUsage(
            DateTime.UtcNow, 50_000m, invalidDesc!, null, request.RequesterId, DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("A description is required for each usage line item.");
    }

    [Fact]
    public void AddAdvanceUsage_OnNonAdvanceRequest_ShouldFail()
    {
        // Arrange: Standard request approved and paid
        var reqId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var request = BudgetRequest.CreateDraft(
            reqId, "Alice", BudgetRequestType.Standard, headId, "Bob",
            DateTime.UtcNow.AddHours(6.5).Date.AddDays(2),
            100_000m, "MMK", "Reasons", "Alice", "Job", Guid.NewGuid(), false, null);

        // Act
        var result = request.AddAdvanceUsage(
            DateTime.UtcNow, 50_000m, "Expense", null, reqId, DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Usage line items can only be recorded on advance requests.");
    }

    // =========================================================================
    // 2. Update & Remove Advance Usage
    // =========================================================================

    [Fact]
    public void UpdateAdvanceUsage_ValidParameters_ShouldUpdateLineItem()
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);
        var now = DateTime.UtcNow;
        var usageId = request.AddAdvanceUsage(now, 100_000m, "Original desc", null, request.RequesterId, now).Value;

        // Act
        var updateResult = request.UpdateAdvanceUsage(
            usageId, now, 120_000m, "Updated desc", null);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        var usage = request.AdvanceUsages.Single();
        usage.Amount.Should().Be(120_000m);
        usage.Description.Should().Be("Updated desc");
        request.TotalUsageRecorded.Should().Be(120_000m);
    }

    [Fact]
    public void UpdateAdvanceUsage_WhenExceedingApprovedAmount_ShouldFail()
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);
        var now = DateTime.UtcNow;
        var usageId = request.AddAdvanceUsage(now, 400_000m, "Original desc", null, request.RequesterId, now).Value;

        // Act: Attempt to increase to 600k (exceeding 500k approved)
        var updateResult = request.UpdateAdvanceUsage(
            usageId, now, 600_000m, "Updated desc", null);

        // Assert
        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Message.Should().Contain("Total usage cannot exceed the approved advance amount");
    }

    [Fact]
    public void RemoveAdvanceUsage_ShouldRemoveItemAndRecalculateTotal()
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);
        var now = DateTime.UtcNow;
        var usageId = request.AddAdvanceUsage(now, 100_000m, "Desc", null, request.RequesterId, now).Value;
        request.AdvanceUsages.Should().ContainSingle();

        // Act
        var removeResult = request.RemoveAdvanceUsage(usageId);

        // Assert
        removeResult.IsSuccess.Should().BeTrue();
        request.AdvanceUsages.Should().BeEmpty();
        request.TotalUsageRecorded.Should().Be(0m);
    }

    // =========================================================================
    // 3. SubmitReconciliation (Three-Way Branching)
    // =========================================================================

    [Fact]
    public void SubmitReconciliation_ExactMatch_ShouldReconcileDirectly()
    {
        // Arrange: Advance 500k, usage exactly 500k
        var request = CreatePendingReconciliationAdvance(500_000m);
        var now = DateTime.UtcNow;
        request.AddAdvanceUsage(now, 500_000m, "Complete trip expenses", null, request.RequesterId, now);

        // Act
        var result = request.SubmitReconciliation(request.RequesterId, now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.Reconciled);
        request.FinalizedAt.Should().Be(now);
        request.RefundAmount.Should().Be(0m);
        request.ReimbursementAmount.Should().Be(0m);
    }

    [Fact]
    public void SubmitReconciliation_UnderSpent_ShouldMoveToAwaitingRefund()
    {
        // Arrange: Advance 500k, usage 400k (unspent 100k)
        var request = CreatePendingReconciliationAdvance(500_000m);
        var now = DateTime.UtcNow;
        request.AddAdvanceUsage(now, 400_000m, "Partial trip expenses", null, request.RequesterId, now);

        // Act
        var result = request.SubmitReconciliation(request.RequesterId, now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.AwaitingRefund);
        request.RefundAmount.Should().Be(100_000m);
        request.FinalizedAt.Should().BeNull(); // Not finalized until refund received
    }

    [Fact]
    public void SubmitReconciliation_WithNoUsagesRecorded_ShouldFail()
    {
        // Arrange: Advance with 0 usages
        var request = CreatePendingReconciliationAdvance(500_000m);

        // Act
        var result = request.SubmitReconciliation(request.RequesterId, DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("At least one usage line item is required before submitting reconciliation.");
    }

    // =========================================================================
    // 4. RecordRefund
    // =========================================================================

    [Fact]
    public void RecordRefund_ExactAmount_ShouldReconcileAndFinalize()
    {
        // Arrange: Awaiting refund of 100k
        var request = CreatePendingReconciliationAdvance(500_000m);
        var now = DateTime.UtcNow;
        var addResult = request.AddAdvanceUsage(now, 400_000m, "Trip expenses", null, request.RequesterId, now);
        addResult.IsSuccess.Should().BeTrue();

        var submitResult = request.SubmitReconciliation(request.RequesterId, now);
        submitResult.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.AwaitingRefund);

        var financeUser = Guid.NewGuid();
        var receiveTime = now; // Valid: >= submittedAt and <= UtcNow + 5min

        // Act
        var result = request.RecordRefund(100_000m, financeUser, receiveTime);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.Reconciled);
        request.RefundReceivedByUserId.Should().Be(financeUser);
        request.RefundReceivedAt.Should().Be(receiveTime);
        request.FinalizedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordRefund_MismatchedAmount_ShouldFail()
    {
        // Arrange: Awaiting refund of 100k
        var request = CreatePendingReconciliationAdvance(500_000m);
        var now = DateTime.UtcNow;
        request.AddAdvanceUsage(now, 400_000m, "Trip", null, request.RequesterId, now);
        request.SubmitReconciliation(request.RequesterId, now);

        // Act: Attempting to refund 50k
        var result = request.RecordRefund(50_000m, Guid.NewGuid(), now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("must exactly match the outstanding refund of 100000");
    }


    // =========================================================================
    // 5. ExtendReconciliationDeadline
    // =========================================================================

    [Fact]
    public void ExtendReconciliationDeadline_LaterDate_ShouldExtendDeadline()
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);
        var currentDeadline = request.ReconciliationDeadline!.Value;
        var extendedDeadline = currentDeadline.AddDays(7);

        // Act
        var result = request.ExtendReconciliationDeadline(extendedDeadline);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.ReconciliationDeadline.Should().Be(extendedDeadline);
    }

    [Fact]
    public void ExtendReconciliationDeadline_EarlierOrEqualDate_ShouldFail()
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);
        var currentDeadline = request.ReconciliationDeadline!.Value;

        // Act 1: Same date
        var sameResult = request.ExtendReconciliationDeadline(currentDeadline);
        sameResult.IsFailure.Should().BeTrue();
        sameResult.Error.Message.Should().Be("The new deadline must be later than the current deadline.");

        // Act 2: Earlier date
        var earlierResult = request.ExtendReconciliationDeadline(currentDeadline.AddDays(-1));
        earlierResult.IsFailure.Should().BeTrue();
        earlierResult.Error.Message.Should().Be("The new deadline must be later than the current deadline.");
    }

    // =========================================================================
    // 6. UndoReconciliationSubmit & UndoRefund Tests
    // =========================================================================

    [Fact]
    public void UndoReconciliationSubmit_FromAwaitingRefund_ShouldResetToPendingReconciliation()
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);
        var now = DateTime.UtcNow;
        request.AddAdvanceUsage(now, 400_000m, "Trip expenses", null, request.RequesterId, now);
        request.SubmitReconciliation(request.RequesterId, now);
        request.Status.Should().Be(RequestStatus.AwaitingRefund);
        request.RefundAmount.Should().Be(100_000m);
        request.ReconciliationSubmittedAt.Should().NotBeNull();

        // Act
        var result = request.UndoReconciliationSubmit(request.RequesterId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PendingReconciliation);
        request.RefundAmount.Should().Be(0m);
        request.ReconciliationSubmittedAt.Should().BeNull();
        request.FinalizedAt.Should().BeNull();
        request.AdvanceUsages.Should().ContainSingle();
        request.TotalUsageRecorded.Should().Be(400_000m);
    }

    [Fact]
    public void UndoReconciliationSubmit_FromExactMatchReconciled_ShouldResetToPendingReconciliation()
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);
        var now = DateTime.UtcNow;
        request.AddAdvanceUsage(now, 500_000m, "Full exact match expenses", null, request.RequesterId, now);
        request.SubmitReconciliation(request.RequesterId, now);
        request.Status.Should().Be(RequestStatus.Reconciled);
        request.RefundAmount.Should().Be(0m);

        // Act
        var result = request.UndoReconciliationSubmit(request.RequesterId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PendingReconciliation);
        request.RefundAmount.Should().Be(0m);
        request.ReconciliationSubmittedAt.Should().BeNull();
        request.FinalizedAt.Should().BeNull();
        request.AdvanceUsages.Should().ContainSingle();
        request.TotalUsageRecorded.Should().Be(500_000m);
    }

    [Fact]
    public void UndoReconciliationSubmit_WhenRefundAlreadyReceived_ShouldFail()
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);
        var now = DateTime.UtcNow;
        request.AddAdvanceUsage(now, 400_000m, "Trip expenses", null, request.RequesterId, now);
        request.SubmitReconciliation(request.RequesterId, now);
        request.RecordRefund(100_000m, Guid.NewGuid(), now);
        request.Status.Should().Be(RequestStatus.Reconciled);
        request.RefundReceivedAt.Should().NotBeNull();

        // Act
        var result = request.UndoReconciliationSubmit(request.RequesterId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Cannot withdraw reconciliation after a refund has been recorded");
    }

    [Fact]
    public void UndoReconciliationSubmit_WhenStatusNotEligible_ShouldFail()
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);
        request.Status.Should().Be(RequestStatus.PendingReconciliation);

        // Act
        var result = request.UndoReconciliationSubmit(request.RequesterId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Reconciliation submission can only be undone");
    }

    [Fact]
    public void UndoRefund_WhenReconciledWithRefund_ShouldResetToAwaitingRefund()
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);
        var now = DateTime.UtcNow;
        request.AddAdvanceUsage(now, 400_000m, "Trip expenses", null, request.RequesterId, now);
        request.SubmitReconciliation(request.RequesterId, now);
        var financeUser = Guid.NewGuid();
        request.RecordRefund(100_000m, financeUser, now);
        request.Status.Should().Be(RequestStatus.Reconciled);
        request.RefundReceivedAt.Should().NotBeNull();
        request.RefundReceivedByUserId.Should().Be(financeUser);

        // Act
        var result = request.UndoRefund(financeUser);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.AwaitingRefund);
        request.RefundReceivedAt.Should().BeNull();
        request.RefundReceivedByUserId.Should().BeNull();
        request.FinalizedAt.Should().BeNull();
        request.RefundAmount.Should().Be(100_000m);
    }

    [Fact]
    public void UndoRefund_WhenNotReconciled_ShouldFail()
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);
        var now = DateTime.UtcNow;
        request.AddAdvanceUsage(now, 400_000m, "Trip expenses", null, request.RequesterId, now);
        request.SubmitReconciliation(request.RequesterId, now);
        request.Status.Should().Be(RequestStatus.AwaitingRefund);

        // Act
        var result = request.UndoRefund(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Refund can only be undone on a reconciled advance");
    }

    [Fact]
    public void UndoRefund_WhenExactMatchWithoutRefund_ShouldFail()
    {
        // Arrange
        var request = CreatePendingReconciliationAdvance(500_000m);
        var now = DateTime.UtcNow;
        request.AddAdvanceUsage(now, 500_000m, "Full exact match expenses", null, request.RequesterId, now);
        request.SubmitReconciliation(request.RequesterId, now);
        request.Status.Should().Be(RequestStatus.Reconciled);
        request.RefundReceivedAt.Should().BeNull();

        // Act
        var result = request.UndoRefund(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("does not have a recorded refund to undo");
    }
}
