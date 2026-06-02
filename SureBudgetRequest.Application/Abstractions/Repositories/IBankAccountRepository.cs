using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface IBankAccountRepository
{
    Task<BankAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all bank accounts. When <paramref name="includeInactive"/> is false (default),
    /// deactivated rows are excluded. Sorted by <c>BankName</c> then <c>AccountNumber</c> ascending.
    /// </summary>
    Task<IReadOnlyList<BankAccount>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task AddAsync(BankAccount account, CancellationToken cancellationToken = default);
}
