using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface IBudgetRequestModificationRepository
{
    Task AddAsync(BudgetRequestModification modification, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BudgetRequestModification>> ListByRequestIdAsync(Guid budgetRequestId, CancellationToken cancellationToken = default);
}
