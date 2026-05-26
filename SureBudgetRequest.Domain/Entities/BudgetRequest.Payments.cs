using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

public partial class BudgetRequest
{
    public Result RecordPayment(
        decimal amount,
        Guid financeUserId,
        DateTime paidAt,
        string? reference,
        string? note,
        Guid? attachmentId = null)
    {
        if (amount <= 0)
            return Result.Failure("Payment amount must be greater than zero.");

        if (Status is not RequestStatus.Approved and not RequestStatus.PartiallyPaid)
            return Result.Failure($"Cannot record payments while status is '{Status}'.");

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

        _payments.Add(new Payment(Id, amount, paidAt, financeUserId, reference, note, attachmentId));

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
}
