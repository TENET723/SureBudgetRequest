namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// Accounting category master record (e.g. "Office Supply", "Subscription", "Marketing Expense").
/// Once a budget request is approved, Accounting assigns an AccountingCategory to it.
/// Payment cannot be recorded until an AccountingCategory is assigned.
/// After the voucher is printed, the assigned category is locked for audit purposes.
/// </summary>
public class AccountingCategory
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // For EF Core
    private AccountingCategory() { }

    public AccountingCategory(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name.Trim();
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name is required.", nameof(newName));
        Name = newName.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
