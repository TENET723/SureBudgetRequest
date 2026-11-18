using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Repositories;

public sealed class AccountingCategoryRepository : IAccountingCategoryRepository
{
    private readonly AppDbContext _context;

    public AccountingCategoryRepository(AppDbContext context) => _context = context;

    public async Task<AccountingCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.AccountingCategories.FindAsync([id], cancellationToken);

    public async Task<AccountingCategory?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalized = name.Trim();
        return await _context.AccountingCategories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == normalized.ToLower(), cancellationToken);
    }

    public async Task<IReadOnlyList<AccountingCategory>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AccountingCategories.AsQueryable();

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AccountingCategory category, CancellationToken cancellationToken = default)
        => await _context.AccountingCategories.AddAsync(category, cancellationToken);
}
