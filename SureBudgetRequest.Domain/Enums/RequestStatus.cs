namespace SureBudgetRequest.Domain.Enums;

public enum RequestStatus
{
    Draft = 0,
    PendingDeptHead = 1,
    PendingManagement = 2,
    PendingFinance = 3,
    SentBack = 4,
    Approved = 5,
    PartiallyPaid = 6,
    Paid = 7,
    Rejected = 8,
    Cancelled = 9,

    // === Advance withdrawal reconciliation phase ===
    // These only ever apply to BudgetRequestType.Advance requests.

    /// <summary>
    /// An advance has been fully paid out and is awaiting the requester's
    /// self-reported usage line items. Replaces <see cref="Paid"/> for advances
    /// — an advance never enters the Paid state.
    /// </summary>
    PendingReconciliation = 10,

    /// <summary>
    /// The requester submitted reconciliation but recorded less usage than the
    /// advance; the unspent difference must be refunded before the request closes.
    /// </summary>
    AwaitingRefund = 11,

    /// <summary>
    /// Terminal state for an advance request: usage fully reconciled (and any
    /// refund received).
    /// </summary>
    Reconciled = 12,

    /// <summary>
    /// The requester submitted reconciliation but recorded more usage than the
    /// advance; the company owes the requester the over-spent difference, which
    /// must be reimbursed before the request closes.
    /// </summary>
    AwaitingReimbursement = 13
}
