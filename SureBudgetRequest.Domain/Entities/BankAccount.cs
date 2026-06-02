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

    /// <summary>Stored as plain text — no masking, no encryption.</summary>
    public string AccountNumber { get; private set; } = null!;
    public string AccountHolderName { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private BankAccount() { }

    public BankAccount(string bankName, string accountNumber, string accountHolderName)
    {
        if (string.IsNullOrWhiteSpace(bankName))
            throw new ArgumentException("Bank name is required.", nameof(bankName));
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new ArgumentException("Account number is required.", nameof(accountNumber));
        if (string.IsNullOrWhiteSpace(accountHolderName))
            throw new ArgumentException("Account holder name is required.", nameof(accountHolderName));

        //Id = Guid.NewGuid();
        BankName = bankName.Trim();
        AccountNumber = accountNumber.Trim();
        AccountHolderName = accountHolderName.Trim();
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(string bankName, string accountNumber, string accountHolderName)
    {
        if (string.IsNullOrWhiteSpace(bankName))
            throw new ArgumentException("Bank name is required.", nameof(bankName));
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new ArgumentException("Account number is required.", nameof(accountNumber));
        if (string.IsNullOrWhiteSpace(accountHolderName))
            throw new ArgumentException("Account holder name is required.", nameof(accountHolderName));

        BankName = bankName.Trim();
        AccountNumber = accountNumber.Trim();
        AccountHolderName = accountHolderName.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
