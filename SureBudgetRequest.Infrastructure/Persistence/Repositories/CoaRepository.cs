using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Repositories;

public sealed class CoaRepository : ICoaRepository
{
    private readonly AppDbContext _context;

    public CoaRepository(AppDbContext context) => _context = context;

    public async Task<Coa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Coas.FindAsync([id], cancellationToken);

    public async Task<Coa?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var normalized = code.Trim();
        // Code uniqueness is case-sensitive (matches entity ctor's behavior).
        return await _context.Coas
            .FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<Coa>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Coas.AsQueryable();

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Coa coa, CancellationToken cancellationToken = default)
        => await _context.Coas.AddAsync(coa, cancellationToken);
}
