namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// A single self-reported usage line item against an advance withdrawal.
/// Recorded by the requester during the reconciliation phase
/// (<see cref="Enums.RequestStatus.PendingReconciliation"/>).
///
/// Follows the same internal-constructor pattern as <see cref="Payment"/> and
/// <see cref="ApprovalAction"/>: only the <see cref="BudgetRequest"/> aggregate
/// can create or mutate one, through the methods in
/// <c>BudgetRequest.Reconciliation.cs</c>.
/// </summary>
public class AdvanceUsage
{
    public Guid Id { get; private set; }
    public Guid BudgetRequestId { get; private set; }

    /// <summary>When the expense actually happened (not when it was recorded).</summary>
    public DateTime SpentOn { get; private set; }

    /// <summary>Amount spent, in the request's currency.</summary>
    public decimal Amount { get; private set; }

    public string Description { get; private set; } = null!;

    /// <summary>Optional FK to the receipt <see cref="Attachment"/> backing this line.</summary>
    public Guid? AttachmentId { get; private set; }

    public DateTime RecordedAt { get; private set; }
    public Guid RecordedByUserId { get; private set; }

    // For EF Core
    private AdvanceUsage() { }

    // Internal: only Domain code (BudgetRequest.AddAdvanceUsage) can create usages.
    // The Id is generated here (rather than left to EF) so AddAdvanceUsage can
    // return it synchronously, before SaveChanges.
    internal AdvanceUsage(
        Guid budgetRequestId,
        DateTime spentOn,
        decimal amount,
        string description,
        Guid? attachmentId,
        DateTime recordedAt,
        Guid recordedByUserId)
    {
        //Id = Guid.NewGuid();
        BudgetRequestId = budgetRequestId;
        SpentOn = spentOn;
        Amount = amount;
        Description = description;
        AttachmentId = attachmentId;
        RecordedAt = recordedAt;
        RecordedByUserId = recordedByUserId;
    }

    /// <summary>
    /// Called ONLY from <see cref="BudgetRequest.UpdateAdvanceUsage"/>. The
    /// aggregate validates the new amount against the advance ceiling before
    /// calling this; <see cref="RecordedAt"/> / <see cref="RecordedByUserId"/>
    /// are intentionally left unchanged (they record the original entry).
    /// </summary>
    internal void Update(DateTime spentOn, decimal amount, string description, Guid? attachmentId)
    {
        SpentOn = spentOn;
        Amount = amount;
        Description = description;
        AttachmentId = attachmentId;
    }
}
