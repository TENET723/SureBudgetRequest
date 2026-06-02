using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Repositories;

public sealed class BankAccountRepository : IBankAccountRepository
{
    private readonly AppDbContext _context;

    public BankAccountRepository(AppDbContext context) => _context = context;

    public async Task<BankAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.BankAccounts.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<BankAccount>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.BankAccounts.AsQueryable();

        if (!includeInactive)
            query = query.Where(a => a.IsActive);

        return await query
            .OrderBy(a => a.BankName)
            .ThenBy(a => a.AccountNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BankAccount account, CancellationToken cancellationToken = default)
        => await _context.BankAccounts.AddAsync(account, cancellationToken);
}
