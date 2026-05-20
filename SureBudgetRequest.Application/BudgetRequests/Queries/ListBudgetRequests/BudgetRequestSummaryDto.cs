using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.ListBudgetRequests;

public sealed record BudgetRequestSummaryDto(
    Guid Id,
    string? Reference,
    Guid RequesterId,
    DateTime RequestDate,
    BudgetRequestType Type,
    decimal RequestedAmount,
    string CurrencyCode,
    decimal ApprovedAmount,
    decimal TotalPaid,
    decimal RequestedAmountInMmkAtSubmission,
    // Monthly-limit snapshot fields — nullable when monthly enforcement
    // wasn't configured for the dept at submission.
    decimal? MonthlyLimitAtSubmission,
    decimal? MonthlySpendBeforeAtSubmission,
    string Reasons,
    RequestStatus Status,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? FinalizedAt,
    // ── Additive fields for the budget-request report (v4+) ─────────────────
    // Added at the end with defaults so existing positional callers
    // (FromEntity) compile unchanged.
    Guid DepartmentIdAtSubmission = default,
    decimal DepartmentLimitAtSubmission = 0m,
    Guid? CoaId = null,
    string? CoaCode = null,
    string? CoaName = null,
    Guid? WithdrawMethodId = null,
    string? WithdrawMethodName = null)
{
    public decimal RemainingBalance => ApprovedAmount - TotalPaid;

    /// <summary>
    /// True when this request's MMK-equivalent at submission exceeded the
    /// department's per-request limit at that time — i.e. it routed through
    /// Management. Computed from the submission snapshots so the answer is
    /// stable even if the department's limit changes later.
    /// </summary>
    public bool IsOverLimit =>
        RequestedAmountInMmkAtSubmission > DepartmentLimitAtSubmission;

    /// <summary>
    /// Existing factory — preserved as-is for backward compatibility. Callers
    /// that don't need COA display fields can keep using this overload. The
    /// new department/COA fields are populated from the entity (Coa lookup
    /// fields are left null and should be filled by the
    /// <see cref="FromEntity(BudgetRequest, Coa?, WithdrawMethod?)"/> overload).
    /// </summary>
    public static BudgetRequestSummaryDto FromEntity(BudgetRequest e) =>
        FromEntity(e, coa: null, withdrawMethod: null);

    /// <summary>
    /// Report-friendly factory. Pass the resolved <see cref="Coa"/> and
    /// <see cref="WithdrawMethod"/> (or null when the corresponding FK is null)
    /// to populate the display fields.
    /// </summary>
    public static BudgetRequestSummaryDto FromEntity(
        BudgetRequest e,
        Coa? coa,
        WithdrawMethod? withdrawMethod) => new(
        e.Id,
        e.Reference,
        e.RequesterId,
        e.RequestDate,
        e.Type,
        e.RequestedAmount,
        e.CurrencyCode,
        e.ApprovedAmount,
        e.TotalPaid,
        e.RequestedAmountInMmkAtSubmission,
        e.MonthlyLimitAtSubmission,
        e.MonthlySpendBeforeAtSubmission,
        e.Reasons,
        e.Status,
        e.CreatedAt,
        e.SubmittedAt,
        e.FinalizedAt,
        e.DepartmentIdAtSubmission,
        e.DepartmentLimitAtSubmission,
        e.CoaId,
        coa?.Code,
        coa?.Name,
        e.WithdrawMethodId,
        withdrawMethod?.Name);
}
