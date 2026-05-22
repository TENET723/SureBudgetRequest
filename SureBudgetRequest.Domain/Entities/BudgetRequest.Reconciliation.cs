using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// Advance-withdrawal reconciliation phase. Once an advance is fully paid it
/// enters <see cref="RequestStatus.PendingReconciliation"/>; the requester then
/// self-reports usage as line items (<see cref="AdvanceUsage"/>) and submits.
/// Recorded usage can never exceed <see cref="ApprovedAmount"/> — overspending
/// is rejected at the entity level by <see cref="AddAdvanceUsage"/> /
/// <see cref="UpdateAdvanceUsage"/>. If less than the advance was used the
/// difference must be refunded before the request reaches the terminal
/// <see cref="RequestStatus.Reconciled"/> state.
/// </summary>
public partial class BudgetRequest
{
    /// <summary>
    /// Records a usage line item against the advance. Application layer enforces
    /// that the caller is the requester; the domain enforces phase, type and the
    /// no-overspending invariant. Returns the new <see cref="AdvanceUsage.Id"/>.
    /// </summary>
    public Result<Guid> AddAdvanceUsage(
        DateTime spentOn,
        decimal amount,
        string description,
        Guid? attachmentId,
        Guid userId,
        DateTime now)
    {
        if (Type != BudgetRequestType.Advance)
            return Result<Guid>.Failure("Usage line items can only be recorded on advance requests.");

        if (Status != RequestStatus.PendingReconciliation)
            return Result<Guid>.Failure(
                $"Usage line items can only be recorded while reconciliation is pending (current status: '{Status}').");

        if (amount <= 0)
            return Result<Guid>.Failure("Usage amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(description))
            return Result<Guid>.Failure("A description is required for each usage line item.");

        if (TotalUsageRecorded + amount > ApprovedAmount)
        {
            var remaining = ApprovedAmount - TotalUsageRecorded;
            return Result<Guid>.Failure(
                $"Recorded usage cannot exceed the advance. Remaining: {remaining}, attempted: {amount}.");
        }

        var usage = new AdvanceUsage(Id, spentOn, amount, description.Trim(), attachmentId, now, userId);
        _advanceUsages.Add(usage);
        return Result<Guid>.Success(usage.Id);
    }

    /// <summary>
    /// Edits an existing usage line item. The no-overspending invariant is
    /// re-checked against the total with the old amount swapped out.
    /// </summary>
    public Result UpdateAdvanceUsage(
        Guid usageId,
        DateTime spentOn,
        decimal amount,
        string description,
        Guid? attachmentId)
    {
        if (Status != RequestStatus.PendingReconciliation)
            return Result.Failure(
                $"Usage line items can only be edited while reconciliation is pending (current status: '{Status}').");

        var usage = _advanceUsages.FirstOrDefault(u => u.Id == usageId);
        if (usage is null)
            return Result.Failure("Usage line item not found on this request.");

        if (amount <= 0)
            return Result.Failure("Usage amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(description))
            return Result.Failure("A description is required for each usage line item.");

        if (TotalUsageRecorded - usage.Amount + amount > ApprovedAmount)
        {
            var remaining = ApprovedAmount - (TotalUsageRecorded - usage.Amount);
            return Result.Failure(
                $"Recorded usage cannot exceed the advance. Remaining: {remaining}, attempted: {amount}.");
        }

        usage.Update(spentOn, amount, description.Trim(), attachmentId);
        return Result.Success();
    }

    /// <summary>Removes a usage line item while reconciliation is still pending.</summary>
    public Result RemoveAdvanceUsage(Guid usageId)
    {
        if (Status != RequestStatus.PendingReconciliation)
            return Result.Failure(
                $"Usage line items can only be removed while reconciliation is pending (current status: '{Status}').");

        var usage = _advanceUsages.FirstOrDefault(u => u.Id == usageId);
        if (usage is null)
            return Result.Failure("Usage line item not found on this request.");

        _advanceUsages.Remove(usage);
        return Result.Success();
    }

    /// <summary>
    /// Finalises the reconciliation. There is no re-approval — usage is
    /// self-reported. If recorded usage exactly matches the advance the request
    /// becomes <see cref="RequestStatus.Reconciled"/>; otherwise it moves to
    /// <see cref="RequestStatus.AwaitingRefund"/> and the unspent balance is
    /// captured in <see cref="RefundAmount"/>. Overspending is impossible —
    /// <see cref="AddAdvanceUsage"/> / <see cref="UpdateAdvanceUsage"/> reject it.
    /// </summary>
    public Result SubmitReconciliation(Guid userId, DateTime now)
    {
        if (Status != RequestStatus.PendingReconciliation)
            return Result.Failure(
                $"Reconciliation can only be submitted while it is pending (current status: '{Status}').");

        if (_advanceUsages.Count < 1)
            return Result.Failure("At least one usage line item is required before submitting reconciliation.");

        ReconciliationSubmittedAt = now;

        if (TotalUsageRecorded == ApprovedAmount)
        {
            Status = RequestStatus.Reconciled;
            FinalizedAt = now;
        }
        else
        {
            // TotalUsageRecorded < ApprovedAmount (overspending cannot happen).
            Status = RequestStatus.AwaitingRefund;
            RefundAmount = ApprovedAmount - TotalUsageRecorded;
        }

        return Result.Success();
    }

    /// <summary>
    /// Records receipt of the outstanding refund. The amount must match
    /// <see cref="RefundAmount"/> exactly — partial refunds are not supported in
    /// v1. On success the advance reaches the terminal
    /// <see cref="RequestStatus.Reconciled"/> state.
    /// </summary>
    public Result RecordRefund(decimal amount, DateTime receivedAt, Guid receivedByUserId)
    {
        if (Status != RequestStatus.AwaitingRefund)
            return Result.Failure(
                $"A refund can only be recorded while the request is awaiting one (current status: '{Status}').");

        if (amount != RefundAmount)
            return Result.Failure(
                $"The refund amount must exactly match the outstanding refund of {RefundAmount}. " +
                "Partial refunds are not supported.");

        RefundReceivedAt = receivedAt;
        RefundReceivedByUserId = receivedByUserId;
        Status = RequestStatus.Reconciled;
        FinalizedAt = receivedAt;

        return Result.Success();
    }

    /// <summary>
    /// Pushes the reconciliation deadline out. Finance-only at the Application
    /// layer; the domain only allows extending (never shortening) so the
    /// requester is never given less time than they already had.
    /// </summary>
    public Result ExtendReconciliationDeadline(DateTime newDeadline)
    {
        if (Status != RequestStatus.PendingReconciliation)
            return Result.Failure(
                $"The reconciliation deadline can only be extended while reconciliation is pending (current status: '{Status}').");

        if (!ReconciliationDeadline.HasValue)
            return Result.Failure("This request has no reconciliation deadline to extend.");

        if (newDeadline <= ReconciliationDeadline.Value)
            return Result.Failure("The new deadline must be later than the current deadline.");

        ReconciliationDeadline = newDeadline;
        return Result.Success();
    }
}
