using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface IVendorRepository
{
    Task<Vendor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Vendor?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vendor>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);
    Task AddAsync(Vendor vendor, CancellationToken cancellationToken = default);
    void AddAccount(VendorPaymentAccount account);
}
