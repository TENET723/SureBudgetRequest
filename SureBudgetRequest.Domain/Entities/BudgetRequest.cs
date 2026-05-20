using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

// FUTURE: Advance withdrawal support (deferred to v2)
// - Add IsAdvance flag at construction time
// - Add reconciliation phase methods after Paid status
// - Add AdvanceExpense child entity (line items)
// - New statuses: PendingReconciliation, ReconciliationApproved, AwaitingRefund, AwaitingTopUp
// - See spec §X (to be written when feature is added)
public partial class BudgetRequest
{
    // === Identity ===
    public Guid Id { get; private set; }

    // Human-friendly reference number, generated at submission time.
    // Format: BR-{TypeCode}-{yyyyMMdd}-{4digit-random}  e.g. "BR-U-20260513-4521"
    //   TypeCode: U = Urgent, S = Standard, P = ProjectProposal
    // Null while the request is in Draft. Stable from Submit() onwards.
    public string? Reference { get; private set; }

    // === Requester input (filled at submission, editable in Draft/SentBack) ===
    public Guid RequesterId { get; private set; }
    public DateTime RequestDate { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public string Reasons { get; private set; } = null!;
    public string WithdrawerName { get; private set; } = null!;
    public string WithdrawerJobTitle { get; private set; } = null!;
    public bool AllowsPartialPayment { get; private set; }
    public string? PartialPaymentDetail { get; private set; }

    /// <summary>
    /// Free-text justification from the requester for why a request that would
    /// push the department over its monthly limit is still necessary. Set on
    /// the entity in Draft/SentBack (via <see cref="CreateDraft"/> /
    /// <see cref="UpdateDetails"/>) and validated at <see cref="Submit"/>-time
    /// — Submit returns Failure if a monthly overrun is detected and this
    /// field is null/whitespace.
    /// </summary>
    public string? MonthlyOverrunJustification { get; private set; }

    // === Currency (editable while Draft/SentBack; locked at Submit via the snapshot below) ===
    // The currency the request is denominated in. Amount, ApprovedAmount, and Payments
    // are all in this currency. Only the limit comparison converts to MMK.
    public string CurrencyCode { get; private set; } = null!;

    // === Submission snapshots (for stable audit/routing) ===
    public Guid DepartmentIdAtSubmission { get; private set; }
    public decimal DepartmentLimitAtSubmission { get; private set; }                // MMK
    public decimal ExchangeRateAtSubmission { get; private set; }                   // CurrencyCode -> MMK at submit time
    public decimal RequestedAmountInMmkAtSubmission { get; private set; }           // RequestedAmount * rate, cached for queries
    public Guid DeptHeadIdAtSubmission { get; private set; }
    public string DeptHeadNameAtSubmission { get; private set; } = null!;
    public string RequesterNameAtSubmission { get; private set; } = null!;

    /// <summary>
    /// The department's monthly limit (in MMK) at the moment this request was
    /// submitted. <c>null</c> when monthly enforcement was not configured for
    /// the department at submission. Used for stable audit display even if
    /// the department's monthly limit changes later.
    /// </summary>
    public decimal? MonthlyLimitAtSubmission { get; private set; }

    /// <summary>
    /// The department's already-spent total (in MMK) for the calendar month
    /// of submission, *before* this request was counted. <c>null</c> when
    /// monthly enforcement was not configured for the department at submission.
    /// Useful for audit display ("how close to the cap was this dept when this
    /// request landed?").
    /// </summary>
    public decimal? MonthlySpendBeforeAtSubmission { get; private set; }

    // === Workflow state ===
    public RequestStatus Status { get; private set; }
    public BudgetRequestType Type { get; private set; }
    public decimal ApprovedAmount { get; private set; }     // 0 until Finance approves; equals RequestedAmount in v1 (in CurrencyCode)
    public DateTime CreatedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public DateTime? FinalizedAt { get; private set; }      // when Paid / Rejected / Cancelled

    /// <summary>
    /// Chart of Account assigned by Finance at approval time. Null until the
    /// Finance stage; required when Finance approves (validated in
    /// <see cref="ApproveBy"/>). Pre-existing approved requests created before
    /// the COA feature shipped remain null indefinitely — there is no backfill.
    ///
    /// Preserved across send-back / re-approval cycles to pre-fill the next
    /// Finance approver's choice. The next Finance approval overwrites it.
    /// </summary>
    public Guid? CoaId { get; private set; }

    /// <summary>
    /// Withdraw method chosen by the requester at draft time (e.g. "Cash",
    /// "Bank Transfer"). Required on every new draft; nullable in DB only so
    /// rows that pre-date the feature can survive without backfill. Editable
    /// while the request is in Draft or SentBack.
    /// </summary>
    public Guid? WithdrawMethodId { get; private set; }

    // === Child collections (part of the aggregate) ===
    private readonly List<ApprovalAction> _approvalActions = new();
    public IReadOnlyList<ApprovalAction> ApprovalActions => _approvalActions.AsReadOnly();

    private readonly List<Payment> _payments = new();
    public IReadOnlyList<Payment> Payments => _payments.AsReadOnly();

    private readonly List<Attachment> _attachments = new();
    public IReadOnlyList<Attachment> Attachments => _attachments.AsReadOnly();

    // === Computed helpers ===
    public decimal TotalPaid => _payments.Sum(p => p.Amount);
    public decimal RemainingBalance => ApprovedAmount - TotalPaid;
    public bool IsTerminal => Status is RequestStatus.Paid
                                    or RequestStatus.Rejected
                                    or RequestStatus.Cancelled;

    // For EF Core
    private BudgetRequest() { }
}
