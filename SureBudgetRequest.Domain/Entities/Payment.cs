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
    public Guid? AttachmentId { get; private set; }

    // Source company bank account — set for bank transfers, null for cash.
    // The account's details are snapshotted onto the payment row so payment
    // history stays stable even if the account is later edited or deactivated.
    public Guid? SourceBankAccountId { get; private set; }
    public string? SourceBankName { get; private set; }
    public string? SourceAccountNumber { get; private set; }
    public string? SourceAccountHolderName { get; private set; }

    // For EF Core
    private Payment() { }

    // Internal: only Domain code (BudgetRequest.RecordPayment) can create payments.
    internal Payment(
        Guid budgetRequestId,
        decimal amount,
        DateTime paidAt,
        Guid recordedByUserId,
        string? reference,
        string? note,
        Guid? attachmentId,
        Guid? sourceBankAccountId,
        string? sourceBankName,
        string? sourceAccountNumber,
        string? sourceAccountHolderName)
    {
        //Id = Guid.NewGuid();
        BudgetRequestId = budgetRequestId;
        Amount = amount;
        PaidAt = paidAt;
        RecordedByUserId = recordedByUserId;
        Reference = reference;
        Note = note;
        AttachmentId = attachmentId;
        SourceBankAccountId = sourceBankAccountId;
        SourceBankName = sourceBankName;
        SourceAccountNumber = sourceAccountNumber;
        SourceAccountHolderName = sourceAccountHolderName;
    }
}
