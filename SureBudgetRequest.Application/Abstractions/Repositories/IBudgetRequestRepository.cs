using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface IBudgetRequestRepository
{
    Task<BudgetRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns requests visible to the given user based on their role:
    /// - Employee: only their own requests.
    /// - DeptHead: requests from their department.
    /// - Boss: all over-limit requests.
    /// - Finance: all requests at PendingFinance or beyond.
    /// - Admin: all requests.
    /// </summary>
    Task<IReadOnlyList<BudgetRequest>> ListAsync(
        Guid? requesterId = null,
        Guid? departmentId = null,
        RequestStatus? status = null,
        IReadOnlyCollection<RequestStatus>? statuses = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(BudgetRequest budgetRequest, CancellationToken cancellationToken = default);
}
