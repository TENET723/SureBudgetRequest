using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface IDepartmentBudgetPeriodRepository
{
    /// <summary>
    /// Returns the configuration in force for <paramref name="financialYear"/> — the
    /// row for this department with the greatest <c>EffectiveFromFinancialYear</c>
    /// that is &lt;= <paramref name="financialYear"/>. Null when the department has
    /// no config effective by that year.
    /// </summary>
    Task<DepartmentBudgetPeriod?> GetActiveAsync(
        Guid departmentId,
        int financialYear,
        CancellationToken cancellationToken = default);

    /// <summary>All config rows for a department, ordered by effective year ascending.</summary>
    Task<IReadOnlyList<DepartmentBudgetPeriod>> ListByDepartmentAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts the row, or updates the existing one when a row already exists for the
    /// same (DepartmentId, EffectiveFromFinancialYear). The caller commits via the
    /// unit of work.
    /// </summary>
    Task UpsertAsync(DepartmentBudgetPeriod period, CancellationToken cancellationToken = default);
}
