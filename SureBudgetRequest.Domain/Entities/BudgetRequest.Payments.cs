using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

public partial class BudgetRequest
{
    public Result RecordPayment(
        decimal amount,
        DateTime paidAt,
        Guid financeUserId,
        string? reference,
        string? note,
        IEnumerable<Guid>? attachmentIds = null,
        Guid? sourceBankAccountId = null,
        string? sourceBankName = null,
        string? sourceAccountNumber = null,
        string? sourceAccountHolderName = null)
    {
        if (amount <= 0)
            return Result.Failure("Payment amount must be greater than zero.");

        if (Status is not RequestStatus.Approved and not RequestStatus.PartiallyPaid)
            return Result.Failure($"Cannot record payments while status is '{Status}'.");

        var financeApproval = ApprovalActions
            .Where(a => a.Stage == ApprovalStage.Finance && a.Decision == ApprovalDecision.Approved)
            .OrderByDescending(a => a.ActionedAt)
            .FirstOrDefault();
        if (financeApproval is not null && paidAt < financeApproval.ActionedAt.AddSeconds(-1))
        {
            return Result.Failure("Payment date cannot be before the request's approval date.");
        }

        if (paidAt > DateTime.UtcNow.AddMinutes(5))
        {
            return Result.Failure("Payment date cannot be in the future.");
        }

        if (!AllowsPartialPayment && amount != ApprovedAmount)
            return Result.Failure(
                "This request does not allow partial payments. The full approved amount must be paid in one transaction.");

        var alreadyPaid = TotalPaid;
        var newTotal = alreadyPaid + amount;

        // R14: hard invariant — never exceed approved amount
        if (newTotal > ApprovedAmount)
        {
            var remaining = ApprovedAmount - alreadyPaid;
            return Result.Failure(
                $"Payment exceeds remaining balance. Remaining: {remaining}, attempted: {amount}.");
        }

        var payment = new Payment(
            Id, amount, paidAt, financeUserId, reference, note,
            sourceBankAccountId, sourceBankName, sourceAccountNumber, sourceAccountHolderName);

        _payments.Add(payment);

        if (attachmentIds is not null)
        {
            var targetList = attachmentIds.ToList();
            if (targetList.Count > MaxAttachmentsPerRequest)
                return Result.Failure($"Cannot attach more than {MaxAttachmentsPerRequest} receipts to a single payment transaction.");

            var attSet = targetList.ToHashSet();
            foreach (var att in _attachments.Where(a => attSet.Contains(a.Id)))
            {
                payment.AddReceipt(att);
                att.AttachToPayment(payment.Id);
            }
        }

        // R15: status auto-transition based on totals
        if (newTotal == ApprovedAmount)
        {
            if (Type == BudgetRequestType.Advance)
            {
                // An advance never reaches Paid. Once fully disbursed it moves
                // straight into the reconciliation phase. FinalizedAt stays null
                // — the request is not final until usage is reconciled (and any
                // refund received).
                Status = RequestStatus.PendingReconciliation;
            }
            else
            {
                Status = RequestStatus.Paid;
                FinalizedAt = DateTime.UtcNow;
            }
        }
        else
        {
            Status = RequestStatus.PartiallyPaid;
        }

        return Result.Success();
    }

    /// <summary>
    /// Voids a recorded payment transaction (soft void for audit preservation).
    /// Only applicable when the request is in <see cref="RequestStatus.PartiallyPaid"/>,
    /// <see cref="RequestStatus.Paid"/>, or <see cref="RequestStatus.PendingReconciliation"/>
    /// (advance with no usages recorded yet).
    /// Reverts status to <see cref="RequestStatus.Approved"/> if no non-voided payments remain,
    /// or to <see cref="RequestStatus.PartiallyPaid"/> if partial balance has still been paid.
    /// </summary>
    public Result VoidPayment(Guid paymentId, Guid financeUserId, DateTime now)
    {
        var payment = _payments.FirstOrDefault(p => p.Id == paymentId);
        if (payment is null)
            return Result.Failure("Payment not found on this request.");

        if (payment.IsVoided)
            return Result.Failure("Payment is already voided.");

        if (Status is not RequestStatus.PartiallyPaid and not RequestStatus.Paid and not RequestStatus.PendingReconciliation)
            return Result.Failure($"Cannot void payment while status is '{Status}'.");

        if (Status == RequestStatus.PendingReconciliation && _advanceUsages.Any())
            return Result.Failure("Cannot void payment because advance usages have already been recorded.");

        payment.MarkVoided(financeUserId, now);

        var remainingPaid = TotalPaid;
        if (remainingPaid == 0)
        {
            Status = RequestStatus.Approved;
            FinalizedAt = null;
        }
        else
        {
            Status = RequestStatus.PartiallyPaid;
            FinalizedAt = null;
        }

        return Result.Success();
    }
}
