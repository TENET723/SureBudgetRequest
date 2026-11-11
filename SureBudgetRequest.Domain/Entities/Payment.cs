namespace SureBudgetRequest.Domain.Entities;

public class Payment
{
    public Guid Id { get; private set; }
    public Guid BudgetRequestId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime PaidAt { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public string? Reference { get; private set; }
    public string? Note { get; private set; }

    // Source company bank account — set for bank transfers, null for cash.
    // The account's details are snapshotted onto the payment row so payment
    // history stays stable even if the account is later edited or deactivated.
    public Guid? SourceBankAccountId { get; private set; }
    public string? SourceBankName { get; private set; }
    public string? SourceAccountNumber { get; private set; }
    public string? SourceAccountHolderName { get; private set; }

    // Destination vendor payment account — nullable (set when paid to a vendor account).
    public Guid? VendorPaymentAccountId { get; private set; }

    // Void tracking (soft void for audit preservation)
    public bool IsVoided { get; private set; }
    public DateTime? VoidedAt { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public string? VoidReason { get; private set; }

    // Navigation — receipt attachments linked to this payment.
    // EF Core uses this to determine INSERT ordering (payment before attachments).
    private readonly List<Attachment> _receipts = new();
    public IReadOnlyList<Attachment> Receipts => _receipts.AsReadOnly();

    // For EF Core
    private Payment() { }

    /// <summary>Links an already-tracked attachment to this payment via navigation.</summary>
    internal void AddReceipt(Attachment attachment) => _receipts.Add(attachment);

    internal void MarkVoided(Guid voidedByUserId, DateTime voidedAt, string voidReason)
    {
        IsVoided = true;
        VoidedByUserId = voidedByUserId;
        VoidedAt = voidedAt;
        VoidReason = voidReason;
    }

    // Internal: only Domain code (BudgetRequest.RecordPayment) can create payments.
    internal Payment(
        Guid budgetRequestId,
        decimal amount,
        DateTime paidAt,
        Guid recordedByUserId,
        string? reference,
        string? note,
        Guid? sourceBankAccountId,
        string? sourceBankName,
        string? sourceAccountNumber,
        string? sourceAccountHolderName,
        Guid? vendorPaymentAccountId = null)
    {
        //Id = Guid.NewGuid();
        BudgetRequestId = budgetRequestId;
        Amount = amount;
        PaidAt = paidAt;
        RecordedAt = DateTime.UtcNow;
        RecordedByUserId = recordedByUserId;
        Reference = reference;
        Note = note;
        SourceBankAccountId = sourceBankAccountId;
        SourceBankName = sourceBankName;
        SourceAccountNumber = sourceAccountNumber;
        SourceAccountHolderName = sourceAccountHolderName;
        VendorPaymentAccountId = vendorPaymentAccountId;
        IsVoided = false;
    }
}
