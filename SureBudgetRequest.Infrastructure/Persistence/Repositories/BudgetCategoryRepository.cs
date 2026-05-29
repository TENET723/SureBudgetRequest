using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Repositories;

public sealed class BudgetCategoryRepository : IBudgetCategoryRepository
{
    private readonly AppDbContext _context;

    public BudgetCategoryRepository(AppDbContext context) => _context = context;

    public async Task<BudgetCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.BudgetCategories.FindAsync([id], cancellationToken);

    public async Task<BudgetCategory?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalized = name.Trim();
        // Name uniqueness is case-insensitive — "Asset" and "asset" should collide
        // since this is a human-facing label.
        return await _context.BudgetCategories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == normalized.ToLower(), cancellationToken);
    }

    public async Task<IReadOnlyList<BudgetCategory>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.BudgetCategories.AsQueryable();

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BudgetCategory category, CancellationToken cancellationToken = default)
        => await _context.BudgetCategories.AddAsync(category, cancellationToken);
}
