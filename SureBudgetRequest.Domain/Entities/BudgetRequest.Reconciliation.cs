using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// Advance-withdrawal reconciliation phase. Once an advance is fully paid it
/// enters <see cref="RequestStatus.PendingReconciliation"/>; the requester then
/// self-reports usage as line items (<see cref="AdvanceUsage"/>) and submits.
/// Total usage cannot exceed the approved advance amount.
/// On submission there are two outcomes: usage equal to the advance reaches the
/// terminal <see cref="RequestStatus.Reconciled"/> state directly; usage less than
/// the advance moves to <see cref="RequestStatus.AwaitingRefund"/> (the requester
/// owes the unspent balance back). Refund is settled by Finance before the request
/// reaches <see cref="RequestStatus.Reconciled"/>.
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
        IEnumerable<Guid>? attachmentIds,
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

        if (TotalUsageRecorded + amount > ApprovedAmount)
        {
            var remaining = Math.Max(0m, ApprovedAmount - TotalUsageRecorded);
            return Result<Guid>.Failure(
                $"Total usage cannot exceed the approved advance amount of {ApprovedAmount:N2} {CurrencyCode}. " +
                $"Remaining allowable spend is {remaining:N2} {CurrencyCode}.");
        }

        if (string.IsNullOrWhiteSpace(description))
            return Result<Guid>.Failure("A description is required for each usage line item.");

        var financeApproval = ApprovalActions
            .Where(a => a.Stage == ApprovalStage.Finance && a.Decision == ApprovalDecision.Approved)
            .OrderByDescending(a => a.ActionedAt)
            .FirstOrDefault();
        if (financeApproval is not null && spentOn < financeApproval.ActionedAt)
        {
            return Result<Guid>.Failure("Expense date cannot be before the request's approval date.");
        }

        if (spentOn > DateTime.UtcNow.AddMinutes(5))
        {
            return Result<Guid>.Failure("Expense date cannot be in the future.");
        }

        var usage = new AdvanceUsage(Id, spentOn, amount, description.Trim(), now, userId);
        _advanceUsages.Add(usage);

        if (attachmentIds is not null)
        {
            var targetIds = attachmentIds.ToList();
            if (targetIds.Count > MaxAttachmentsPerRequest)
                return Result<Guid>.Failure($"Cannot attach more than {MaxAttachmentsPerRequest} receipts to a single usage line item.");

            foreach (var attachmentId in targetIds)
            {
                var att = _attachments.FirstOrDefault(a => a.Id == attachmentId);
                if (att is not null)
                {
                    usage.AddReceipt(att);
                    att.AttachToAdvanceUsage(usage.Id);
                }
            }
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
        IEnumerable<Guid>? attachmentIds)
    {
        if (Status != RequestStatus.PendingReconciliation)
            return Result.Failure(
                $"Usage line items can only be edited while reconciliation is pending (current status: '{Status}').");

        var usage = _advanceUsages.FirstOrDefault(u => u.Id == usageId);
        if (usage is null)
            return Result.Failure("Usage line item not found on this request.");

        if (amount <= 0)
            return Result.Failure("Usage amount must be greater than zero.");

        var otherUsagesTotal = _advanceUsages.Where(u => u.Id != usageId).Sum(u => u.Amount);
        if (otherUsagesTotal + amount > ApprovedAmount)
        {
            var remaining = Math.Max(0m, ApprovedAmount - otherUsagesTotal);
            return Result.Failure(
                $"Total usage cannot exceed the approved advance amount of {ApprovedAmount:N2} {CurrencyCode}. " +
                $"Remaining allowable spend is {remaining:N2} {CurrencyCode}.");
        }

        if (string.IsNullOrWhiteSpace(description))
            return Result.Failure("A description is required for each usage line item.");

        var financeApproval = ApprovalActions
            .Where(a => a.Stage == ApprovalStage.Finance && a.Decision == ApprovalDecision.Approved)
            .OrderByDescending(a => a.ActionedAt)
            .FirstOrDefault();
        if (financeApproval is not null && spentOn < financeApproval.ActionedAt)
        {
            return Result.Failure("Expense date cannot be before the request's approval date.");
        }

        if (spentOn > DateTime.UtcNow.AddMinutes(5))
        {
            return Result.Failure("Expense date cannot be in the future.");
        }

        usage.Update(spentOn, amount, description.Trim());

        var targetList = (attachmentIds ?? Enumerable.Empty<Guid>()).ToList();
        if (targetList.Count > MaxAttachmentsPerRequest)
            return Result.Failure($"Cannot attach more than {MaxAttachmentsPerRequest} receipts to a single usage line item.");

        var targetIds = targetList.ToHashSet();

        // Remove attachments that are no longer in targetIds
        var detachedAtts = _attachments.Where(a => a.AdvanceUsageId == usageId && !targetIds.Contains(a.Id)).ToList();
        foreach (var att in detachedAtts)
        {
            _attachments.Remove(att);
        }

        // Attach target attachments to this usage
        foreach (var attId in targetIds)
        {
            var att = _attachments.FirstOrDefault(a => a.Id == attId);
            if (att is not null)
            {
                if (!usage.Receipts.Any(r => r.Id == attId))
                {
                    usage.AddReceipt(att);
                }
                att.AttachToAdvanceUsage(usage.Id);
            }
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

        var attachedFiles = _attachments.Where(a => a.AdvanceUsageId == usageId).ToList();
        foreach (var file in attachedFiles)
        {
            _attachments.Remove(file);
        }

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
            return Result.Failure(
                $"Total usage ({TotalUsageRecorded:N2} {CurrencyCode}) exceeds the approved advance amount ({ApprovedAmount:N2} {CurrencyCode}). Overspending is not permitted.");
        }

        return Result.Success();
    }

    /// <summary>
    /// Records receipt of the outstanding refund. The amount must match
    /// <see cref="RefundAmount"/> exactly — partial refunds are not supported in
    /// v1. On success the advance reaches the terminal
    /// <see cref="RequestStatus.Reconciled"/> state.
    /// </summary>
    public Result RecordRefund(decimal amount, Guid receivedByUserId, DateTime receivedAt)
    {
        if (Status != RequestStatus.AwaitingRefund)
            return Result.Failure(
                $"A refund can only be recorded while the request is awaiting one (current status: '{Status}').");

        if (amount != RefundAmount)
            return Result.Failure(
                $"The refund amount must exactly match the outstanding refund of {RefundAmount}. " +
                "Partial refunds are not supported.");

        if (ReconciliationSubmittedAt.HasValue && receivedAt < ReconciliationSubmittedAt.Value)
        {
            return Result.Failure("Refund date cannot be before the reconciliation was submitted.");
        }

        if (receivedAt > DateTime.UtcNow.AddMinutes(5))
        {
            return Result.Failure("Refund date cannot be in the future.");
        }

        RefundReceivedAt = receivedAt;
        RefundReceivedByUserId = receivedByUserId;
        Status = RequestStatus.Reconciled;
        FinalizedAt = DateTime.UtcNow;

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
