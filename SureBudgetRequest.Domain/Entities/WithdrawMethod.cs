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

    /// <summary>
    /// When true, the requester must attach at least one banking-info file
    /// (<see cref="Enums.AttachmentCategory.WithdrawMethod"/>) before they
    /// can submit. Enforced inside <c>BudgetRequest.Submit</c>.
    /// </summary>
    public bool RequiresAttachment { get; private set; }

    private WithdrawMethod() { }

    public WithdrawMethod(string name, bool requiresAttachment = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        //Id = Guid.NewGuid();
        Name = name.Trim();
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        RequiresAttachment = requiresAttachment;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name is required.", nameof(newName));
        Name = newName.Trim();
    }

    public void SetRequiresAttachment(bool value) => RequiresAttachment = value;

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
