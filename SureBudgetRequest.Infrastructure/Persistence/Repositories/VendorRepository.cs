using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Repositories;

public sealed class VendorRepository : IVendorRepository
{
    private readonly AppDbContext _context;

    public VendorRepository(AppDbContext context) => _context = context;

    public async Task<Vendor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Vendors
            .Include(v => v.PaymentAccounts)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<Vendor?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim().ToLower();
        return await _context.Vendors
            .Include(v => v.PaymentAccounts)
            .FirstOrDefaultAsync(v => v.Name.ToLower() == trimmed, cancellationToken);
    }

    public async Task<IReadOnlyList<Vendor>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Vendors
            .Include(v => v.PaymentAccounts)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(v => v.IsActive);

        return await query
            .OrderBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Vendor vendor, CancellationToken cancellationToken = default)
        => await _context.Vendors.AddAsync(vendor, cancellationToken);

    public void AddAccount(VendorPaymentAccount account)
        => _context.VendorPaymentAccounts.Add(account);
}
