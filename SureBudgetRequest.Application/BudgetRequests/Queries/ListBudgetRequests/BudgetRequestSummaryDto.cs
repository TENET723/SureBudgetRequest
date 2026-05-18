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
    DateTime? FinalizedAt)
{
    public decimal RemainingBalance => ApprovedAmount - TotalPaid;

    public static BudgetRequestSummaryDto FromEntity(BudgetRequest e) => new(
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
        e.FinalizedAt);
}
