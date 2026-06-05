using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Repositories;

public sealed class DepartmentMonthlyBudgetRepository : IDepartmentMonthlyBudgetRepository
{
    private readonly AppDbContext _db;

    public DepartmentMonthlyBudgetRepository(AppDbContext db) => _db = db;

    public async Task<DepartmentMonthlyBudget?> GetAsync(
        Guid departmentId, int year, int month, CancellationToken ct)
        => await _db.DepartmentMonthlyBudgets
            .SingleOrDefaultAsync(b =>
                b.DepartmentId == departmentId &&
                b.Year == year &&
                b.Month == month, ct);

    public async Task<IReadOnlyList<DepartmentMonthlyBudget>> ListByDepartmentYearAsync(
        Guid departmentId, int year, CancellationToken ct)
        => await _db.DepartmentMonthlyBudgets
            .Where(b => b.DepartmentId == departmentId && b.Year == year)
            .ToListAsync(ct);

    public async Task AddAsync(DepartmentMonthlyBudget budget, CancellationToken ct)
        => await _db.DepartmentMonthlyBudgets.AddAsync(budget, ct);
}
