namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// Withdraw method master record. Each budget request is tagged with one
/// WithdrawMethod by the requester at draft time so Finance knows how the
/// money should be disbursed (e.g. "Cash", "Bank Transfer", "Cheque").
///
/// Mirrors the <see cref="Coa"/> soft-delete pattern: rows are deactivated
/// rather than deleted so historical budget requests retain their reference.
/// </summary>
public class WithdrawMethod
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private WithdrawMethod() { }

    public WithdrawMethod(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Id = Guid.NewGuid();
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
