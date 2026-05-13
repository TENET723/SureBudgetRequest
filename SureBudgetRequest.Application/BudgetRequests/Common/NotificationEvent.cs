using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Common;

/// <summary>
/// Describes a single notification to be sent after a status change.
/// The Infrastructure layer maps this to a Slack message.
/// </summary>
public sealed record NotificationEvent(
    NotificationTrigger Trigger,
    Guid BudgetRequestId,
    string BudgetRequestTitle,   // e.g. "Budget Request #1234"
    decimal RequestedAmount,
    Guid RecipientUserId,
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
}
