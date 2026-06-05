namespace SureBudgetRequest.Domain.Entities;

public class DepartmentMonthlyBudget
{
    public Guid Id { get; private set; }
    public Guid DepartmentId { get; private set; }
    public int Year { get; private set; }        // financial year start year
    public int Month { get; private set; }       // calendar month 1-12
    public decimal Amount { get; private set; }  // MMK, >= 0
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private DepartmentMonthlyBudget() { }  // EF Core

    public DepartmentMonthlyBudget(Guid departmentId, int year, int month,
        decimal amount, Guid createdByUserId)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));
        if (month < 1 || month > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        if (year <= 2000)
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be greater than 2000.");

        Id = Guid.NewGuid();
        DepartmentId = departmentId;
        Year = year;
        Month = month;
        Amount = amount;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(decimal newAmount, Guid updatedByUserId)
    {
        if (newAmount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(newAmount));

        Amount = newAmount;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }
}
