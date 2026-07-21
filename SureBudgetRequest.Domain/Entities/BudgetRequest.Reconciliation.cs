using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// Advance-withdrawal reconciliation phase. Once an advance is fully paid it
/// enters <see cref="RequestStatus.PendingReconciliation"/>; the requester then
/// self-reports usage as line items (<see cref="AdvanceUsage"/>) and submits.
/// Usage is fully self-reported, with no cap — it may be less than, equal to, or
/// greater than <see cref="ApprovedAmount"/>. On submission there are three
/// outcomes: usage equal to the advance reaches the terminal
/// <see cref="RequestStatus.Reconciled"/> state directly; usage less than the
/// advance moves to <see cref="RequestStatus.AwaitingRefund"/> (the requester
/// owes the unspent balance back); usage greater than the advance moves to
/// <see cref="RequestStatus.AwaitingReimbursement"/> (the company owes the
/// requester the over-spent difference). Refund / reimbursement are settled by
/// Finance before the request reaches <see cref="RequestStatus.Reconciled"/>.
/// </summary>
public partial class BudgetRequest
{
    /// <summary>
    /// Records a usage line item against the advance. Application layer enforces
    /// that the caller is the requester; the domain enforces phase and type.
    /// Usage is self-reported and uncapped — recording more than the advance is
    /// permitted and resolves to a reimbursement at submission time. Returns the
    /// new <see cref="AdvanceUsage.Id"/>.
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

        var usage = new AdvanceUsage(Id, spentOn, amount, description.Trim(), now, userId);
        _advanceUsages.Add(usage);

        if (attachmentId.HasValue)
        {
            var att = _attachments.FirstOrDefault(a => a.Id == attachmentId.Value);
            if (att is not null) usage.AddReceipt(att);
        }

        return Result<Guid>.Success(usage.Id);
    }

    /// <summary>
    /// Edits an existing usage line item. Usage is uncapped — the edited total
    /// may exceed the advance, resolving to a reimbursement at submission time.
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

        usage.Update(spentOn, amount, description.Trim());

        if (attachmentId.HasValue)
        {
            var att = _attachments.FirstOrDefault(a => a.Id == attachmentId.Value);
            if (att is not null) usage.AddReceipt(att);
        }

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
    /// self-reported. Three outcomes depending on recorded usage vs the advance:
    /// exact match → <see cref="RequestStatus.Reconciled"/>; under-spend →
    /// <see cref="RequestStatus.AwaitingRefund"/> with the unspent balance
    /// captured in <see cref="RefundAmount"/>; over-spend →
    /// <see cref="RequestStatus.AwaitingReimbursement"/> with the over-spent
    /// difference captured in <see cref="ReimbursementAmount"/>.
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
        else if (TotalUsageRecorded < ApprovedAmount)
        {
            // Under-spent: the requester owes the unspent balance back.
            Status = RequestStatus.AwaitingRefund;
            RefundAmount = ApprovedAmount - TotalUsageRecorded;
        }
        else
        {
            // Over-spent: the company owes the requester the difference.
            Status = RequestStatus.AwaitingReimbursement;
            ReimbursementAmount = TotalUsageRecorded - ApprovedAmount;
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
    /// Records payment of the outstanding reimbursement on an advance that
    /// reconciled for more than it disbursed. The amount must match
    /// <see cref="ReimbursementAmount"/> exactly — partial reimbursements are not
    /// supported. On success the advance reaches the terminal
    /// <see cref="RequestStatus.Reconciled"/> state.
    /// </summary>
    public Result RecordReimbursement(decimal amount, DateTime paidAt, Guid paidByUserId)
    {
        if (Status != RequestStatus.AwaitingReimbursement)
            return Result.Failure(
                $"A reimbursement can only be recorded while the request is awaiting one (current status: '{Status}').");

        if (amount != ReimbursementAmount)
            return Result.Failure(
                $"The reimbursement amount must exactly match the outstanding reimbursement of {ReimbursementAmount}. " +
                "Partial reimbursements are not supported.");

        ReimbursementPaidAt = paidAt;
        ReimbursementPaidByUserId = paidByUserId;
        Status = RequestStatus.Reconciled;
        FinalizedAt = paidAt;

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
