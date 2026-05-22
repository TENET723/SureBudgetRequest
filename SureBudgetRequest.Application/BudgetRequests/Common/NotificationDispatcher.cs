using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Common;

internal sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;

    public NotificationDispatcher(
        IUserRepository userRepository,
        INotificationService notificationService)
    {
        _userRepository = userRepository;
        _notificationService = notificationService;
    }

    public async Task DispatchAsync(
        BudgetRequest request,
        RequestStatus previousStatus,
        Guid actorId,
        string? actorName,
        string? comment,
        CancellationToken cancellationToken)
    {
        var evt = await BuildEventAsync(
            request, previousStatus, actorName, comment, cancellationToken);
        if (evt is null) return;

        await _notificationService.SendAsync(evt, cancellationToken);
    }

    private async Task<NotificationEvent?> BuildEventAsync(
        BudgetRequest request,
        RequestStatus previousStatus,
        string? actorName,
        string? comment,
        CancellationToken ct)
    {
        var requesterName = request.RequesterNameAtSubmission;

        NotificationTrigger? trigger = null;
        IReadOnlyList<Guid> recipients = Array.Empty<Guid>();

        // --- Submission ---
        if (previousStatus is RequestStatus.Draft or RequestStatus.SentBack
            && request.Status != RequestStatus.Draft)
        {
            switch (request.Status)
            {
                // Dept head self-submitted, under limit → straight to Finance
                case RequestStatus.PendingFinance
                    when request.DeptHeadIdAtSubmission == request.RequesterId:
                    trigger = NotificationTrigger.SubmittedToFinance;
                    recipients = await GetFinanceApproverIdsAsync(ct);
                    break;

                // Dept head self-submitted, over limit → Management
                case RequestStatus.PendingManagement
                    when request.DeptHeadIdAtSubmission == request.RequesterId:
                    trigger = NotificationTrigger.SubmittedToManagement;
                    recipients = await GetUserIdsByRoleAsync(UserRole.Management, ct);
                    break;

                // Normal submission → dept head
                case RequestStatus.PendingDeptHead:
                    trigger = NotificationTrigger.SubmittedToDeptHead;
                    recipients = new[] { request.DeptHeadIdAtSubmission };
                    break;
            }
        }
        // --- Dept Head approved ---
        else if (previousStatus == RequestStatus.PendingDeptHead
                 && request.Status is RequestStatus.PendingManagement or RequestStatus.PendingFinance)
        {
            if (request.Status == RequestStatus.PendingManagement)
            {
                trigger = NotificationTrigger.DeptHeadApprovedToManagement;
                recipients = await GetUserIdsByRoleAsync(UserRole.Management, ct);
            }
            else
            {
                trigger = NotificationTrigger.DeptHeadApprovedToFinance;
                recipients = await GetFinanceApproverIdsAsync(ct);
            }
        }
        // --- Dept Head rejected ---
        else if (previousStatus == RequestStatus.PendingDeptHead
                 && request.Status == RequestStatus.Rejected)
        {
            trigger = NotificationTrigger.DeptHeadRejectedToRequester;
            recipients = new[] { request.RequesterId };
        }
        // --- Management approved ---
        else if (previousStatus == RequestStatus.PendingManagement
                 && request.Status == RequestStatus.PendingFinance)
        {
            trigger = NotificationTrigger.ManagementApprovedToFinance;
            recipients = await GetFinanceApproverIdsAsync(ct);
        }
        // --- Management rejected ---
        else if (previousStatus == RequestStatus.PendingManagement
                 && request.Status == RequestStatus.Rejected)
        {
            trigger = NotificationTrigger.ManagementRejectedToRequester;
            recipients = new[] { request.RequesterId };
        }
        // --- Finance approved ---
        else if (previousStatus == RequestStatus.PendingFinance
                 && request.Status == RequestStatus.Approved)
        {
            // Advances get a distinct message that names the reconciliation
            // deadline Finance just set.
            trigger = request.Type == BudgetRequestType.Advance
                ? NotificationTrigger.AdvanceApprovedToRequester
                : NotificationTrigger.FinanceApprovedToRequester;
            recipients = new[] { request.RequesterId };
        }
        // --- Finance marked paid ---
        else if (previousStatus is RequestStatus.Approved or RequestStatus.PartiallyPaid
                 && request.Status == RequestStatus.Paid)
        {
            trigger = NotificationTrigger.FinancePaidToRequester;
            recipients = new[] { request.RequesterId };
        }
        // --- Finance sent back ---
        else if (previousStatus == RequestStatus.PendingFinance
                 && request.Status == RequestStatus.SentBack)
        {
            trigger = NotificationTrigger.FinanceSentBackToRequester;
            recipients = new[] { request.RequesterId };
        }

        if (trigger is null) return null;

        return new NotificationEvent(
            trigger.Value,
            request.Id,
            recipients,
            requesterName,
            actorName,
            comment);
    }

    public async Task DispatchReconciliationSubmittedAsync(
        BudgetRequest request,
        string? actorName,
        CancellationToken cancellationToken)
    {
        var financeIds = await GetFinanceApproverIdsAsync(cancellationToken);

        // Always: the requester submitted a reconciliation → ping Finance.
        await _notificationService.SendAsync(
            new NotificationEvent(
                NotificationTrigger.ReconciliationSubmittedToFinance,
                request.Id,
                financeIds,
                request.RequesterNameAtSubmission,
                actorName),
            cancellationToken);

        // If a refund is now owed, also ping the requester + Finance.
        if (request.Status == RequestStatus.AwaitingRefund)
        {
            var recipients = financeIds
                .Append(request.RequesterId)
                .Distinct()
                .ToArray();

            await _notificationService.SendAsync(
                new NotificationEvent(
                    NotificationTrigger.AdvanceAwaitingRefund,
                    request.Id,
                    recipients,
                    request.RequesterNameAtSubmission,
                    actorName),
                cancellationToken);
        }
    }

    public async Task DispatchRefundRecordedAsync(
        BudgetRequest request,
        string? actorName,
        CancellationToken cancellationToken)
    {
        await _notificationService.SendAsync(
            new NotificationEvent(
                NotificationTrigger.RefundRecordedToRequester,
                request.Id,
                new[] { request.RequesterId },
                request.RequesterNameAtSubmission,
                actorName),
            cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> GetUserIdsByRoleAsync(
        UserRole role, CancellationToken ct)
    {
        var users = await _userRepository.ListAsync(role: role, cancellationToken: ct);
        return users.Select(u => u.Id).ToArray();
    }

    /// <summary>
    /// Returns IDs of active Finance users with <c>IsFinanceApprover = true</c>.
    /// Payer-only (Type 2) Finance users are excluded — they don't need to be
    /// pinged when something hits the Finance approval queue.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> GetFinanceApproverIdsAsync(CancellationToken ct)
    {
        var users = await _userRepository.ListAsync(
            role: UserRole.Finance,
            isFinanceApprover: true,
            cancellationToken: ct);
        return users.Select(u => u.Id).ToArray();
    }
}
