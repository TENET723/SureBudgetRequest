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
        string? note)
    {
        if (amount <= 0)
            return Result.Failure("Payment amount must be greater than zero.");

        if (Status is not RequestStatus.Approved and not RequestStatus.PartiallyPaid)
            return Result.Failure($"Cannot record payments while status is '{Status}'.");

        var alreadyPaid = TotalPaid;
        var newTotal = alreadyPaid + amount;

        // R14: hard invariant — never exceed approved amount
        if (newTotal > ApprovedAmount)
        {
            var remaining = ApprovedAmount - alreadyPaid;
            return Result.Failure(
                $"Payment exceeds remaining balance. Remaining: {remaining}, attempted: {amount}.");
        }

        _payments.Add(new Payment(Id, amount, paidAt, financeUserId, reference, note));

        // R15: status auto-transition based on totals
        if (newTotal == ApprovedAmount)
        {
            Status = RequestStatus.Paid;
            FinalizedAt = DateTime.UtcNow;
        }
        else
        {
            Status = RequestStatus.PartiallyPaid;
        }

        return Result.Success();
    }
}
