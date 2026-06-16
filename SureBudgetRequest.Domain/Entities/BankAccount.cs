namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// Company bank account master record. Finance picks one of these as the source
/// account when recording a bank-transfer payment so the disbursement is traceable
/// to a specific company account.
///
/// Mirrors the <see cref="WithdrawMethod"/> soft-delete pattern: rows are
/// deactivated rather than deleted so historical payments retain their reference.
/// </summary>
public class BankAccount
{
    public Guid Id { get; private set; }
    public string BankName { get; private set; } = null!;

    /// <summary>Optional. Stored as plain text — no masking, no encryption.</summary>
    public string? AccountNumber { get; private set; }

    /// <summary>Optional. The account owner / holder name.</summary>
    public string? AccountHolderName { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private BankAccount() { }

    public BankAccount(string bankName, string? accountNumber, string? accountHolderName)
    {
        if (string.IsNullOrWhiteSpace(bankName))
            throw new ArgumentException("Bank name is required.", nameof(bankName));

        //Id = Guid.NewGuid();
        BankName = bankName.Trim();
        AccountNumber = string.IsNullOrWhiteSpace(accountNumber) ? null : accountNumber.Trim();
        AccountHolderName = string.IsNullOrWhiteSpace(accountHolderName) ? null : accountHolderName.Trim();
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(string bankName, string? accountNumber, string? accountHolderName)
    {
        if (string.IsNullOrWhiteSpace(bankName))
            throw new ArgumentException("Bank name is required.", nameof(bankName));

        BankName = bankName.Trim();
        AccountNumber = string.IsNullOrWhiteSpace(accountNumber) ? null : accountNumber.Trim();
        AccountHolderName = string.IsNullOrWhiteSpace(accountHolderName) ? null : accountHolderName.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
