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
    /// in <see cref="RequestStatus.AwaitingRefund"/> — additionally pings the
    /// requester and Finance about the refund that is now due.
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
    /// Notifies the requester that a payment record was voided by Finance.
    /// Call AFTER <c>BudgetRequest.VoidPayment</c>.
    /// </summary>
    Task DispatchPaymentVoidedAsync(
        BudgetRequest request,
        decimal amount,
        string currencyCode,
        string? actorName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Notifies the requester that an approval was undone.
    /// Call AFTER <c>BudgetRequest.UndoApproval</c>.
    /// </summary>
    Task DispatchApprovalUndoneAsync(
        BudgetRequest request,
        string stageName,
        string? actorName,
        string? comment,
        CancellationToken cancellationToken);

    /// <summary>
    /// Notifies reviewers that a submitted request was withdrawn back to Draft by the requester.
    /// Call AFTER <c>BudgetRequest.UndoSubmit</c>.
    /// </summary>
    Task DispatchSubmissionUndoneAsync(
        BudgetRequest request,
        RequestStatus previousStatus,
        string? actorName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Notifies parties that reconciliation submission was withdrawn or reopened back to PendingReconciliation.
    /// Call AFTER <c>BudgetRequest.UndoReconciliationSubmit</c>.
    /// </summary>
    Task DispatchReconciliationSubmissionUndoneAsync(
        BudgetRequest request,
        string? actorName,
        bool isFinanceActor,
        CancellationToken cancellationToken);

    /// <summary>
    /// Notifies the requester that Finance undid the recorded refund.
    /// Call AFTER <c>BudgetRequest.UndoRefund</c>.
    /// </summary>
    Task DispatchRefundUndoneAsync(
        BudgetRequest request,
        string? actorName,
        CancellationToken cancellationToken);
}
