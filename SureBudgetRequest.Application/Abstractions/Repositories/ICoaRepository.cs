using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface ICoaRepository
{
    Task<Coa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Coa?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all Coas. When <paramref name="includeInactive"/> is false (default),
    /// deactivated rows are excluded. Sorted by <c>Code</c> ascending.
    /// </summary>
    Task<IReadOnlyList<Coa>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task AddAsync(Coa coa, CancellationToken cancellationToken = default);
}
