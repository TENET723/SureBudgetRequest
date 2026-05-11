namespace SureBudgetRequest.Domain.Entities;

public class Department
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public Guid? HeadUserId { get; private set; }
    public decimal BudgetLimit { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // For EF Core
    private Department() { }

    public Department(string name, Guid? headUserId, decimal budgetLimit)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Department name is required.", nameof(name));
        if (budgetLimit < 0)
            throw new ArgumentException("Budget limit cannot be negative.", nameof(budgetLimit));

        Id = Guid.NewGuid();
        Name = name;
        HeadUserId = headUserId;
        BudgetLimit = budgetLimit;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Department name is required.", nameof(newName));
        Name = newName;
    }

    public void ChangeHead(Guid? newHeadUserId) => HeadUserId = newHeadUserId;

    public void ChangeBudgetLimit(decimal newLimit)
    {
        if (newLimit < 0)
            throw new ArgumentException("Budget limit cannot be negative.", nameof(newLimit));
        BudgetLimit = newLimit;
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
