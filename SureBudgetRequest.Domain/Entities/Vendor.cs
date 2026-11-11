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
    /// If marked as default, or if this is the only active account, it becomes the default account.
    /// </summary>
    public VendorPaymentAccount AddPaymentAccount(
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
        var hasActiveAccounts = _paymentAccounts.Any(a => a.IsActive);
        var shouldBeDefault = isDefault || !hasActiveAccounts;

        if (shouldBeDefault)
        {
            foreach (var existing in _paymentAccounts)
            {
                existing.SetDefault(false);
            }
        }

        var account = new VendorPaymentAccount(
            Id,
            accountType,
            providerName,
            accountNumber,
            accountHolderName,
            branchName,
            shouldBeDefault,
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

    public void SetDefaultPaymentAccount(Guid accountId)
    {
        var target = GetAccountOrThrow(accountId);
        if (!target.IsActive)
            throw new InvalidOperationException("Cannot set an inactive payment account as default.");

        foreach (var account in _paymentAccounts)
        {
            account.SetDefault(account.Id == accountId);
        }
    }

    public void DeactivatePaymentAccount(Guid accountId)
    {
        var account = GetAccountOrThrow(accountId);
        var wasDefault = account.IsDefault;
        account.Deactivate();

        if (wasDefault)
        {
            var nextActive = _paymentAccounts.FirstOrDefault(a => a.IsActive && a.Id != accountId);
            if (nextActive is not null)
            {
                nextActive.SetDefault(true);
            }
        }
    }

    public void ReactivatePaymentAccount(Guid accountId)
    {
        var account = GetAccountOrThrow(accountId);
        account.Reactivate();

        // If no active account is currently default, make this one default
        if (!_paymentAccounts.Any(a => a.IsActive && a.IsDefault))
        {
            account.SetDefault(true);
        }
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
