using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Common;

/// <summary>
/// Translates a domain event (status changed on a BudgetRequest) into the
/// correct <see cref="NotificationEvent"/> per the §9 routing table and
/// dispatches it via <see cref="INotificationService"/>.
///
/// Called by command handlers after every successful state transition.
/// </summary>
internal static class NotificationDispatcher
{
    public static async Task DispatchAsync(
        BudgetRequest request,
        RequestStatus previousStatus,
        Guid actorId,
        string? comment,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var evt = BuildEvent(request, previousStatus, actorId, comment);
        if (evt is null) return;

        await notificationService.SendAsync(evt, cancellationToken);
    }

    private static NotificationEvent? BuildEvent(
        BudgetRequest request,
        RequestStatus previousStatus,
        Guid actorId,
        string? comment)
    {
        var title = $"Budget Request ({request.RequestedAmount:N0} MMK)";

        // --- Submission ---
        if (previousStatus is RequestStatus.Draft or RequestStatus.SentBack
            && request.Status != RequestStatus.Draft)
        {
            return request.Status switch
            {
                // Went straight to Finance (dept head self-submitted, under limit)
                RequestStatus.PendingFinance when request.DeptHeadIdAtSubmission == request.RequesterId
                    && request.BossIdAtSubmission is null =>
                    new NotificationEvent(
                        NotificationTrigger.SubmittedToFinance,
                        request.Id, title, request.RequestedAmount,
                        RecipientMustBeResolvedByInfrastructure_Finance()),

                // Went to Boss (dept head self-submitted, over limit)
                RequestStatus.PendingBoss when request.DeptHeadIdAtSubmission == request.RequesterId =>
                    new NotificationEvent(
                        NotificationTrigger.SubmittedToBoss,
                        request.Id, title, request.RequestedAmount,
                        request.BossIdAtSubmission!.Value),

                // Normal submission → dept head
                RequestStatus.PendingDeptHead =>
                    new NotificationEvent(
                        NotificationTrigger.SubmittedToDeptHead,
                        request.Id, title, request.RequestedAmount,
                        request.DeptHeadIdAtSubmission),

                _ => null
            };
        }

        // --- Dept Head approved ---
        if (previousStatus == RequestStatus.PendingDeptHead
            && request.Status is RequestStatus.PendingBoss or RequestStatus.PendingFinance)
        {
            return request.Status == RequestStatus.PendingBoss
                ? new NotificationEvent(NotificationTrigger.DeptHeadApprovedToBoss,
                    request.Id, title, request.RequestedAmount,
                    request.BossIdAtSubmission!.Value)
                : new NotificationEvent(NotificationTrigger.DeptHeadApprovedToFinance,
                    request.Id, title, request.RequestedAmount,
                    RecipientMustBeResolvedByInfrastructure_Finance());
        }

        // --- Dept Head rejected ---
        if (previousStatus == RequestStatus.PendingDeptHead
            && request.Status == RequestStatus.Rejected)
        {
            return new NotificationEvent(NotificationTrigger.DeptHeadRejectedToRequester,
                request.Id, title, request.RequestedAmount,
                request.RequesterId, comment);
        }

        // --- Boss approved ---
        if (previousStatus == RequestStatus.PendingBoss
            && request.Status == RequestStatus.PendingFinance)
        {
            return new NotificationEvent(NotificationTrigger.BossApprovedToFinance,
                request.Id, title, request.RequestedAmount,
                RecipientMustBeResolvedByInfrastructure_Finance());
        }

        // --- Boss rejected ---
        if (previousStatus == RequestStatus.PendingBoss
            && request.Status == RequestStatus.Rejected)
        {
            return new NotificationEvent(NotificationTrigger.BossRejectedToRequester,
                request.Id, title, request.RequestedAmount,
                request.RequesterId, comment);
        }

        // --- Finance approved ---
        if (previousStatus == RequestStatus.PendingFinance
            && request.Status == RequestStatus.Approved)
        {
            return new NotificationEvent(NotificationTrigger.FinanceApprovedToRequester,
                request.Id, title, request.RequestedAmount,
                request.RequesterId);
        }

        // --- Finance marked paid ---
        if (previousStatus is RequestStatus.Approved or RequestStatus.PartiallyPaid
            && request.Status == RequestStatus.Paid)
        {
            return new NotificationEvent(NotificationTrigger.FinancePaidToRequester,
                request.Id, title, request.RequestedAmount,
                request.RequesterId);
        }

        // --- Finance sent back ---
        if (previousStatus == RequestStatus.PendingFinance
            && request.Status == RequestStatus.SentBack)
        {
            return new NotificationEvent(NotificationTrigger.FinanceSentBackToRequester,
                request.Id, title, request.RequestedAmount,
                request.RequesterId, comment);
        }

        return null;
    }

    /// <summary>
    /// Finance notifications have no fixed recipient ID — any Finance user can act.
    /// We use Guid.Empty as a sentinel; the Infrastructure Slack service resolves
    /// this to the Finance channel/group.
    /// </summary>
    private static Guid RecipientMustBeResolvedByInfrastructure_Finance() => Guid.Empty;
}
