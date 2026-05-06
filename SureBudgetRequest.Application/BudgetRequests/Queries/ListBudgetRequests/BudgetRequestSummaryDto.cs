using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.ListBudgetRequests;

public sealed record BudgetRequestSummaryDto(
    Guid Id,
    Guid RequesterId,
    DateTime RequestDate,
    decimal RequestedAmount,
    string Reasons,
    RequestStatus Status,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? FinalizedAt)
{
    public static BudgetRequestSummaryDto FromEntity(BudgetRequest e) => new(
        e.Id,
        e.RequesterId,
        e.RequestDate,
        e.RequestedAmount,
        e.Reasons,
        e.Status,
        e.CreatedAt,
        e.SubmittedAt,
        e.FinalizedAt);
}
