using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// Effective-dated cumulative-spending configuration for a department. Each row
/// declares the cadence (<see cref="PeriodType"/>) and recurring cap
/// (<see cref="LimitAmount"/>, in MMK) that take effect from
/// <see cref="EffectiveFromFinancialYear"/> onward, until a later row supersedes it.
///
/// The ACTIVE config for any financial year is the row with the greatest
/// <see cref="EffectiveFromFinancialYear"/> that is &lt;= that year. There is at most
/// one row per department per financial year (enforced by a unique index).
///
/// <see cref="PeriodType"/> = <see cref="BudgetPeriodType.None"/> means the
/// department has no cumulative cap from that financial year onward;
/// <see cref="LimitAmount"/> is then ignored.
/// </summary>
public class DepartmentBudgetPeriod
{
    public Guid Id { get; private set; }
    public Guid DepartmentId { get; private set; }
    public int EffectiveFromFinancialYear { get; private set; }
    public BudgetPeriodType PeriodType { get; private set; }

    /// <summary>Recurring cap per period, in MMK. Ignored when <see cref="PeriodType"/> is None.</summary>
    public decimal LimitAmount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // For EF Core
    private DepartmentBudgetPeriod() { }

    public DepartmentBudgetPeriod(
        Guid departmentId,
        int effectiveFromFinancialYear,
        BudgetPeriodType periodType,
        decimal limitAmount)
    {
        if (departmentId == Guid.Empty)
            throw new ArgumentException("Department id is required.", nameof(departmentId));
        if (limitAmount < 0)
            throw new ArgumentException("Limit amount cannot be negative.", nameof(limitAmount));

        //Id = Guid.NewGuid();
        DepartmentId = departmentId;
        EffectiveFromFinancialYear = effectiveFromFinancialYear;
        PeriodType = periodType;
        // Normalize: a "None" cadence carries no meaningful amount.
        LimitAmount = periodType == BudgetPeriodType.None ? 0m : limitAmount;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the cadence and amount in place. Used when an admin changes the
    /// config for a financial year that already has a row (upsert).
    /// </summary>
    public void Update(BudgetPeriodType periodType, decimal limitAmount)
    {
        if (limitAmount < 0)
            throw new ArgumentException("Limit amount cannot be negative.", nameof(limitAmount));
        PeriodType = periodType;
        LimitAmount = periodType == BudgetPeriodType.None ? 0m : limitAmount;
    }
}
