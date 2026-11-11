using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// A destination payment account belonging to a vendor (e.g. KBZ Bank account, KBZPay wallet, WavePay).
/// A single vendor can have multiple payment accounts.
/// </summary>
public class VendorPaymentAccount
{
    public Guid Id { get; private set; }
    public Guid VendorId { get; private set; }
    public PaymentAccountType AccountType { get; private set; }
    public string ProviderName { get; private set; } = null!;
    public string AccountNumber { get; private set; } = null!;
    public string AccountHolderName { get; private set; } = null!;
    public string? BranchName { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Optional banking proof or QR code attachment (e.g. KBZPay QR screenshot, bank book photo).
    /// Uploaded once on the account and reused across requests.
    /// </summary>
    public string? AttachmentFileName { get; private set; }
    public string? AttachmentStoredPath { get; private set; }
    public string? AttachmentContentType { get; private set; }
    public long? AttachmentSizeBytes { get; private set; }

    public bool HasAttachment => !string.IsNullOrEmpty(AttachmentStoredPath);

    private VendorPaymentAccount() { }

    internal VendorPaymentAccount(
        Guid vendorId,
        PaymentAccountType accountType,
        string providerName,
        string accountNumber,
        string accountHolderName,
        string? branchName = null,
        bool isDefault = false,
        string? attachmentFileName = null,
        string? attachmentStoredPath = null,
        string? attachmentContentType = null,
        long? attachmentSizeBytes = null)
    {
        if (vendorId == Guid.Empty)
            throw new ArgumentException("VendorId is required.", nameof(vendorId));
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name is required.", nameof(providerName));
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new ArgumentException("Account number or phone number is required.", nameof(accountNumber));
        if (string.IsNullOrWhiteSpace(accountHolderName))
            throw new ArgumentException("Account holder name or username is required.", nameof(accountHolderName));

        Id = Guid.NewGuid();
        VendorId = vendorId;
        AccountType = accountType;
        ProviderName = providerName.Trim();
        AccountNumber = accountNumber.Trim();
        AccountHolderName = accountHolderName.Trim();
        BranchName = string.IsNullOrWhiteSpace(branchName) ? null : branchName.Trim();
        IsDefault = isDefault;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;

        SetAttachment(attachmentFileName, attachmentStoredPath, attachmentContentType, attachmentSizeBytes);
    }

    internal void Update(
        PaymentAccountType accountType,
        string providerName,
        string accountNumber,
        string accountHolderName,
        string? branchName = null)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name is required.", nameof(providerName));
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new ArgumentException("Account number or phone number is required.", nameof(accountNumber));
        if (string.IsNullOrWhiteSpace(accountHolderName))
            throw new ArgumentException("Account holder name or username is required.", nameof(accountHolderName));

        AccountType = accountType;
        ProviderName = providerName.Trim();
        AccountNumber = accountNumber.Trim();
        AccountHolderName = accountHolderName.Trim();
        BranchName = string.IsNullOrWhiteSpace(branchName) ? null : branchName.Trim();
    }

    internal void SetDefault(bool isDefault) => IsDefault = isDefault;

    internal void Deactivate()
    {
        IsActive = false;
        IsDefault = false;
    }

    internal void Reactivate() => IsActive = true;

    internal void SetAttachment(
        string? fileName,
        string? storedPath,
        string? contentType,
        long? sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            RemoveAttachment();
            return;
        }

        AttachmentFileName = string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim();
        AttachmentStoredPath = storedPath.Trim();
        AttachmentContentType = string.IsNullOrWhiteSpace(contentType) ? null : contentType.Trim();
        AttachmentSizeBytes = sizeBytes;
    }

    internal void RemoveAttachment()
    {
        AttachmentFileName = null;
        AttachmentStoredPath = null;
        AttachmentContentType = null;
        AttachmentSizeBytes = null;
    }
}
