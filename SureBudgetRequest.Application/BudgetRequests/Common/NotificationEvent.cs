using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Common;

/// <summary>
/// Describes a single notification to be sent after a status change.
/// Kept lean — the outbox processor loads the BudgetRequest and Department
/// fresh at send time, so request fields (reference, reasons, withdrawer
/// info, partial-payment details, etc.) are NOT carried on the event.
/// Only fields specific to the transition itself live here: who acted,
/// who needs to be notified, and any comment from the actor.
///
/// RecipientUserIds is always a list (size 1 for direct notifications,
/// N for role-based ones like Finance/Management). The list is snapshotted
/// at dispatch time so adding a user to a role later doesn't retroactively
/// ping them about old events.
/// </summary>
public sealed record NotificationEvent(
    NotificationTrigger Trigger,
    Guid BudgetRequestId,
    IReadOnlyList<Guid> RecipientUserIds,
    string RequesterName,
    string? ActorName,               // null for plain submissions (actor == requester)
    string? Comment = null);

/// <summary>
/// Maps 1-to-1 with the "Event → Recipient" table in §9 of the requirements.
/// </summary>
public enum NotificationTrigger
{
    // Submission outcomes
    SubmittedToDeptHead,
    SubmittedToFinance,          // dept head submitted, under limit
    SubmittedToManagement,       // dept head submitted, over limit

    // Dept Head stage
    DeptHeadApprovedToManagement,
    DeptHeadApprovedToFinance,
    DeptHeadRejectedToRequester,

    // Management stage
    ManagementApprovedToFinance,
    ManagementRejectedToRequester,

    // Finance stage
    FinanceApprovedToRequester,
    FinancePaidToRequester,
    FinanceSentBackToRequester,

    // === Advance withdrawal — reconciliation phase ===

    /// <summary>Finance approved an advance and set its reconciliation deadline → requester.</summary>
    AdvanceApprovedToRequester,

    /// <summary>The requester submitted their reconciliation → Finance.</summary>
    ReconciliationSubmittedToFinance,

    /// <summary>Reconciliation landed in AwaitingRefund (usage &lt; advance) → requester + Finance.</summary>
    AdvanceAwaitingRefund,

    /// <summary>Finance recorded the refund; the advance is now reconciled → requester.</summary>
    RefundRecordedToRequester,
}
