using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Repositories;

public sealed class DepartmentBudgetPeriodRepository : IDepartmentBudgetPeriodRepository
{
    private readonly AppDbContext _context;

    public DepartmentBudgetPeriodRepository(AppDbContext context) => _context = context;

    public async Task<DepartmentBudgetPeriod?> GetActiveAsync(
        Guid departmentId,
        int financialYear,
        CancellationToken cancellationToken = default)
    {
        // The config in force for the given year = the greatest EffectiveFromFinancialYear <= year.
        return await _context.DepartmentBudgetPeriods
            .Where(p => p.DepartmentId == departmentId
                     && p.EffectiveFromFinancialYear <= financialYear)
            .OrderByDescending(p => p.EffectiveFromFinancialYear)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DepartmentBudgetPeriod>> ListByDepartmentAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DepartmentBudgetPeriods
            .Where(p => p.DepartmentId == departmentId)
            .OrderBy(p => p.EffectiveFromFinancialYear)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(DepartmentBudgetPeriod period, CancellationToken cancellationToken = default)
    {
        var existing = await _context.DepartmentBudgetPeriods
            .FirstOrDefaultAsync(p => p.DepartmentId == period.DepartmentId
                                   && p.EffectiveFromFinancialYear == period.EffectiveFromFinancialYear,
                cancellationToken);

        if (existing is null)
            await _context.DepartmentBudgetPeriods.AddAsync(period, cancellationToken);
        else
            existing.Update(period.PeriodType, period.LimitAmount);
    }
}
