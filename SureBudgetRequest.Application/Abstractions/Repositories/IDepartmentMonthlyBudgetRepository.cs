using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface IDepartmentMonthlyBudgetRepository
{
    Task<DepartmentMonthlyBudget?> GetAsync(
        Guid departmentId, int year, int month, CancellationToken ct);

    Task<IReadOnlyList<DepartmentMonthlyBudget>> ListByDepartmentYearAsync(
        Guid departmentId, int year, CancellationToken ct);

    Task AddAsync(DepartmentMonthlyBudget budget, CancellationToken ct);
}
