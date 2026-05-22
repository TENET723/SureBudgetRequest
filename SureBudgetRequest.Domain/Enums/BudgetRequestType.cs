namespace SureBudgetRequest.Domain.Enums;
public enum BudgetRequestType
{
    Urgent = 0,
    Standard = 1,
    ProjectProposal = 2,

    /// <summary>
    /// Advance withdrawal: the requester draws money before knowing exactly how
    /// it will be spent. Finance sets a reconciliation deadline at approval time;
    /// after the request is fully paid it enters a reconciliation phase where the
    /// requester self-reports usage line items and refunds any unspent balance.
    /// </summary>
    Advance = 3,
}
