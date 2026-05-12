using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Repositories;

public sealed class CurrencyRepository : ICurrencyRepository
{
    private readonly AppDbContext _context;

    public CurrencyRepository(AppDbContext context) => _context = context;

    public async Task<Currency?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var normalized = code.Trim().ToUpperInvariant();
        return await _context.Currencies.FindAsync([normalized], cancellationToken);
    }

    public async Task<IReadOnlyList<Currency>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Currencies.AsQueryable();

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Currency currency, CancellationToken cancellationToken = default)
        => await _context.Currencies.AddAsync(currency, cancellationToken);

    public async Task AddRateChangeAsync(
        CurrencyRateChange change, CancellationToken cancellationToken = default)
        => await _context.CurrencyRateChanges.AddAsync(change, cancellationToken);

    public async Task<IReadOnlyList<CurrencyRateChange>> ListRateChangesAsync(
        string? currencyCode = null,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CurrencyRateChanges.AsQueryable();

        if (!string.IsNullOrWhiteSpace(currencyCode))
        {
            var normalized = currencyCode.Trim().ToUpperInvariant();
            query = query.Where(c => c.CurrencyCode == normalized);
        }

        query = query.OrderByDescending(c => c.ChangedAt);

        if (take.HasValue)
            query = query.Take(take.Value);

        return await query.ToListAsync(cancellationToken);
    }
}
