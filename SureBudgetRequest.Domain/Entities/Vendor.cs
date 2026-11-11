using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// Vendor master record. Maintained by Finance/Admin so payees and their payment accounts
/// (banks, mobile wallets like KBZPay) can be registered once and reused across budget requests.
/// </summary>
public class Vendor
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? ContactPerson { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<VendorPaymentAccount> _paymentAccounts = new();
    public IReadOnlyList<VendorPaymentAccount> PaymentAccounts => _paymentAccounts.AsReadOnly();

    private Vendor() { }

    public Vendor(
        string name,
        string? contactPerson = null,
        string? phone = null,
        string? email = null,
        string? address = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Vendor name is required.", nameof(name));

        Id = Guid.NewGuid();
        Name = name.Trim();
        ContactPerson = string.IsNullOrWhiteSpace(contactPerson) ? null : contactPerson.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(
        string name,
        string? contactPerson = null,
        string? phone = null,
        string? email = null,
        string? address = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Vendor name is required.", nameof(name));

        Name = name.Trim();
        ContactPerson = string.IsNullOrWhiteSpace(contactPerson) ? null : contactPerson.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;

    /// <summary>
    /// Adds a new payment account to this vendor.
    /// </summary>
    public VendorPaymentAccount AddPaymentAccount(
        PaymentAccountType accountType,
        string providerName,
        string accountNumber,
        string accountHolderName,
        string? branchName = null,
        string? attachmentFileName = null,
        string? attachmentStoredPath = null,
        string? attachmentContentType = null,
        long? attachmentSizeBytes = null)
    {
        var account = new VendorPaymentAccount(
            Id,
            accountType,
            providerName,
            accountNumber,
            accountHolderName,
            branchName,
            attachmentFileName,
            attachmentStoredPath,
            attachmentContentType,
            attachmentSizeBytes);

        _paymentAccounts.Add(account);
        return account;
    }

    public void UpdatePaymentAccount(
        Guid accountId,
        PaymentAccountType accountType,
        string providerName,
        string accountNumber,
        string accountHolderName,
        string? branchName = null)
    {
        var account = GetAccountOrThrow(accountId);
        account.Update(accountType, providerName, accountNumber, accountHolderName, branchName);
    }

    public void DeactivatePaymentAccount(Guid accountId)
    {
        var account = GetAccountOrThrow(accountId);
        account.Deactivate();
    }

    public void ReactivatePaymentAccount(Guid accountId)
    {
        var account = GetAccountOrThrow(accountId);
        account.Reactivate();
    }

    public void SetAccountAttachment(
        Guid accountId,
        string fileName,
        string storedPath,
        string contentType,
        long sizeBytes)
    {
        var account = GetAccountOrThrow(accountId);
        account.SetAttachment(fileName, storedPath, contentType, sizeBytes);
    }

    public void RemoveAccountAttachment(Guid accountId)
    {
        var account = GetAccountOrThrow(accountId);
        account.RemoveAttachment();
    }

    private VendorPaymentAccount GetAccountOrThrow(Guid accountId)
    {
        return _paymentAccounts.FirstOrDefault(a => a.Id == accountId)
            ?? throw new InvalidOperationException($"Payment account with id '{accountId}' not found on vendor '{Name}'.");
    }
}
