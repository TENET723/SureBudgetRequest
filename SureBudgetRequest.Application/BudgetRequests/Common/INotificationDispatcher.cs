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

    /// <summary>
    /// Fires the advance-reconciliation submission notifications: always pings
    /// Finance that a reconciliation was submitted, and — when the request landed
    /// in <see cref="RequestStatus.AwaitingRefund"/> or
    /// <see cref="RequestStatus.AwaitingReimbursement"/> — additionally pings the
    /// requester and Finance about the refund or reimbursement that is now due.
    /// Call AFTER <c>BudgetRequest.SubmitReconciliation</c> has succeeded.
    /// </summary>
    Task DispatchReconciliationSubmittedAsync(
        BudgetRequest request,
        string? actorName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Notifies the requester that the outstanding refund was recorded and the
    /// advance is now reconciled. Call AFTER <c>BudgetRequest.RecordRefund</c>.
    /// </summary>
    Task DispatchRefundRecordedAsync(
        BudgetRequest request,
        string? actorName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Notifies the requester that the outstanding reimbursement was paid and the
    /// advance is now reconciled. Call AFTER <c>BudgetRequest.RecordReimbursement</c>.
    /// </summary>
    Task DispatchReimbursementRecordedAsync(
        BudgetRequest request,
        string? actorName,
        CancellationToken cancellationToken);
}
