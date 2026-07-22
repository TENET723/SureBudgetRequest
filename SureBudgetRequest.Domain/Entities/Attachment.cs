using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

// Placeholder. Will be wired up when we add file storage in Infrastructure.
// Following the same internal-constructor pattern as ApprovalAction and Payment:
// attachments are added through BudgetRequest.AddAttachment(...).
public class Attachment
{
    public Guid Id { get; private set; }
    public Guid BudgetRequestId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string StoredPath { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTime UploadedAt { get; private set; }

    /// <summary>
    /// Discriminator for what kind of supporting file this is. Defaults to
    /// <see cref="AttachmentCategory.General"/> for backward compatibility with
    /// rows that pre-date the category column.
    /// </summary>
    public AttachmentCategory Category { get; private set; }

    /// <summary>
    /// Optional FK linking this attachment to a specific <see cref="Payment"/> record when Category is PaymentReceipt.
    /// </summary>
    public Guid? PaymentId { get; private set; }

    /// <summary>
    /// Optional FK linking this attachment to a specific <see cref="AdvanceUsage"/> line item when Category is UsageReceipt.
    /// </summary>
    public Guid? AdvanceUsageId { get; private set; }

    // For EF Core
    private Attachment() { }

    internal Attachment(
        Guid budgetRequestId,
        string fileName,
        string storedPath,
        string contentType,
        long sizeBytes,
        Guid uploadedByUserId,
        AttachmentCategory category = AttachmentCategory.General,
        Guid? paymentId = null,
        Guid? advanceUsageId = null)
    {
        //Id = Guid.NewGuid();
        BudgetRequestId = budgetRequestId;
        FileName = fileName;
        StoredPath = storedPath;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        UploadedByUserId = uploadedByUserId;
        UploadedAt = DateTime.UtcNow;
        Category = category;
        PaymentId = paymentId;
        AdvanceUsageId = advanceUsageId;
    }

    internal void AttachToPayment(Guid paymentId)
    {
        PaymentId = paymentId;
    }

    internal void AttachToAdvanceUsage(Guid? advanceUsageId)
    {
        AdvanceUsageId = advanceUsageId;
    }
}
