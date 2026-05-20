using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Repositories;

public sealed class WithdrawMethodRepository : IWithdrawMethodRepository
{
    private readonly AppDbContext _context;

    public WithdrawMethodRepository(AppDbContext context) => _context = context;

    public async Task<WithdrawMethod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.WithdrawMethods.FindAsync([id], cancellationToken);

    public async Task<WithdrawMethod?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalized = name.Trim();
        // Name uniqueness is case-insensitive — "Cash" and "cash" should collide
        // since this is a human-facing label.
        return await _context.WithdrawMethods
            .FirstOrDefaultAsync(m => m.Name.ToLower() == normalized.ToLower(), cancellationToken);
    }

    public async Task<IReadOnlyList<WithdrawMethod>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.WithdrawMethods.AsQueryable();

        if (!includeInactive)
            query = query.Where(m => m.IsActive);

        return await query
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(WithdrawMethod method, CancellationToken cancellationToken = default)
        => await _context.WithdrawMethods.AddAsync(method, cancellationToken);
}
