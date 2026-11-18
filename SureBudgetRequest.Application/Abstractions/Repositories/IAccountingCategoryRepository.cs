using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface IAccountingCategoryRepository
{
    Task<AccountingCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AccountingCategory?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all accounting categories. When <paramref name="includeInactive"/> is false (default),
    /// deactivated rows are excluded. Sorted by <c>Name</c> ascending.
    /// </summary>
    Task<IReadOnlyList<AccountingCategory>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task AddAsync(AccountingCategory category, CancellationToken cancellationToken = default);
}
