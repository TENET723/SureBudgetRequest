namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// Budget category master record. An optional, admin-managed label that the
/// requester (and the department head, while the request is in
/// <see cref="Enums.RequestStatus.PendingDeptHead"/>) can attach to a budget
/// request — for example "Asset" or "Expense". Purely descriptive: no routing,
/// finance, or chart-of-account logic branches on it.
///
/// Mirrors the <see cref="WithdrawMethod"/> / <see cref="Coa"/> soft-delete
/// pattern: rows are deactivated rather than deleted so historical budget
/// requests retain their reference.
/// </summary>
public class BudgetCategory
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // For EF Core
    private BudgetCategory() { }

    public BudgetCategory(string name)
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
