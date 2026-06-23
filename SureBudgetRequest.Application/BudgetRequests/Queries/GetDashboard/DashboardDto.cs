using SureBudgetRequest.Application.BudgetRequests.Queries.ListBudgetRequests;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.GetDashboard;

/// <summary>
/// Aggregated data for the role-aware dashboard at "/".
///
/// Sections are nullable — populated only when relevant to the current user's role:
///  - PendingMyApproval : DepartmentHead | Management | Finance
///  - PaymentQueue      : Finance | Accounting
///  - SettlementQueue   : Finance | Accounting (advances awaiting refund / reimbursement)
///  - FinanceMetrics    : Finance only
///  - StuckRequests     : Admin only
///
/// The Employee section is always populated for every role.
/// </summary>
public sealed record DashboardDto(
    EmployeeSectionDto Employee,
    ApprovalQueueDto? MyApprovalQueue,
    PaymentQueueDto? PaymentQueue,
    SettlementQueueDto? SettlementQueue,
    FinanceMetricsDto? FinanceMetrics,
    StuckRequestsDto? StuckRequests,
    // Lookup table so cards can show requester names without extra round-trips.
    IReadOnlyDictionary<Guid, string> RequesterNames);

public sealed record EmployeeSectionDto(
    // Drafts + SentBack — things the user needs to do something about
    IReadOnlyList<BudgetRequestSummaryDto> ActionRequired,
    // Non-terminal requests submitted by the user (full list, newest first)
    IReadOnlyList<BudgetRequestSummaryDto> InFlight,
    // Paid/Rejected/Cancelled by the user (full list, newest first)
    IReadOnlyList<BudgetRequestSummaryDto> RecentlyCompleted);

public sealed record ApprovalQueueDto(
    int TotalCount,
    // FULL list (TotalCount == TopItems.Count), sorted Urgent-first then
    // oldest-first. The dashboard table paginates client-side; the card count
    // must always match the rows in the matching tab.
    IReadOnlyList<BudgetRequestSummaryDto> TopItems);

public sealed record PaymentQueueDto(
    int TotalCount,
    IReadOnlyList<BudgetRequestSummaryDto> TopItems);

/// <summary>
/// Advances that have been reconciled into a balance and now need Finance to
/// record the closing money movement — a refund received from the requester
/// (<see cref="SureBudgetRequest.Domain.Enums.RequestStatus.AwaitingRefund"/>)
/// or a reimbursement paid to them
/// (<see cref="SureBudgetRequest.Domain.Enums.RequestStatus.AwaitingReimbursement"/>).
/// Both directions share one queue. Allowed for any Finance user (both
/// sub-types); shown read-only to Accounting.
/// </summary>
public sealed record SettlementQueueDto(
    int TotalCount,
    IReadOnlyList<BudgetRequestSummaryDto> TopItems);

public sealed record FinanceMetricsDto(
    int PendingApprovalCount,
    int AwaitingPaymentCount,
    decimal OutstandingTotalMmk);   // sum of RemainingBalance converted to MMK at the submission-time rate

public sealed record StuckRequestsDto(
    int TotalCount,
    IReadOnlyList<BudgetRequestSummaryDto> TopItems);
