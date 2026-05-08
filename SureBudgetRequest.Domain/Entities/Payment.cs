namespace SureBudgetRequest.Domain.Entities;

public class Payment
{
    public Guid Id { get; private set; }
    public Guid BudgetRequestId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime PaidAt { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public string? Reference { get; private set; }
    public string? Note { get; private set; }

    // For EF Core
    private Payment() { }

    // Internal: only Domain code (BudgetRequest.RecordPayment) can create payments.
    internal Payment(
        Guid budgetRequestId,
        decimal amount,
        DateTime paidAt,
        Guid recordedByUserId,
        string? reference,
        string? note)
    {
        //Id = Guid.NewGuid();
        BudgetRequestId = budgetRequestId;
        Amount = amount;
        PaidAt = paidAt;
        RecordedByUserId = recordedByUserId;
        Reference = reference;
        Note = note;
    }
}
