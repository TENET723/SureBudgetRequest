using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface IWithdrawMethodRepository
{
    Task<WithdrawMethod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WithdrawMethod?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all withdraw methods. When <paramref name="includeInactive"/> is false (default),
    /// deactivated rows are excluded. Sorted by <c>Name</c> ascending.
    /// </summary>
    Task<IReadOnlyList<WithdrawMethod>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task AddAsync(WithdrawMethod method, CancellationToken cancellationToken = default);
}
