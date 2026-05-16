using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Common;

/// <summary>
/// Resolves a status transition into the correct NotificationEvent and
/// hands it to the INotificationService for outbox storage.
///
/// Replaces the previous static NotificationDispatcher; now a scoped
/// service because it needs IUserRepository to resolve role-based
/// recipient lists (Finance, Management).
/// </summary>
public interface INotificationDispatcher
{
    Task DispatchAsync(
        BudgetRequest request,
        RequestStatus previousStatus,
        Guid actorId,
        string? actorName,
        string? comment,
        CancellationToken cancellationToken);
}
