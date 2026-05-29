using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface IBudgetCategoryRepository
{
    Task<BudgetCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BudgetCategory?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all budget categories. When <paramref name="includeInactive"/> is false (default),
    /// deactivated rows are excluded. Sorted by <c>Name</c> ascending.
    /// </summary>
    Task<IReadOnlyList<BudgetCategory>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task AddAsync(BudgetCategory category, CancellationToken cancellationToken = default);
}
