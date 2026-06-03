namespace SureBudgetRequest.Application.BudgetRequests.Queries.SearchBudgetRequests;

/// <summary>
/// Sortable columns for the paged budget-request search. Maps to entity
/// columns in <c>BudgetRequestRepository.SearchAsync</c>:
/// <c>AmountInMmk</c> → <c>RequestedAmountInMmkAtSubmission</c>,
/// <c>Requester</c> → <c>RequesterNameAtSubmission</c>.
/// </summary>
public enum BudgetRequestSortBy
{
    SubmittedAt,
    RequestDate,
    Reference,
    Requester,
    Type,
    AmountInMmk,
    Status
}
