using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

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
    /// push the department over its budget-period limit is still necessary. Set on
    /// the entity in Draft/SentBack (via <see cref="CreateDraft"/> /
    /// <see cref="UpdateDetails"/>) and validated at <see cref="Submit"/>-time
    /// — Submit returns Failure if a period overrun is detected and this
    /// field is null/whitespace. (Field name retained from the original
    /// monthly-only design; now generic across all period cadences.)
    /// </summary>
    public string? MonthlyOverrunJustification { get; private set; }

    // === Currency (editable while Draft/SentBack; locked at Submit via the snapshot below) ===
    // The currency the request is denominated in. Amount, ApprovedAmount, and Payments
    // are all in this currency. Only the limit comparison converts to MMK.
    public string CurrencyCode { get; private set; } = null!;

    /// <summary>
    /// Optional manual exchange rate override. If set, this rate is used during
    /// submission instead of the system rate. Only allowed for non-Advance requests.
    /// </summary>
    public decimal? ManualExchangeRate { get; private set; }

    // === Submission snapshots (for stable audit/routing) ===
    public Guid DepartmentIdAtSubmission { get; private set; }
    public decimal DepartmentLimitAtSubmission { get; private set; }                // MMK
    public decimal ExchangeRateAtSubmission { get; private set; }                   // CurrencyCode -> MMK at submit time
    public decimal RequestedAmountInMmkAtSubmission { get; private set; }           // RequestedAmount * rate, cached for queries
    public Guid DeptHeadIdAtSubmission { get; private set; }
    public string DeptHeadNameAtSubmission { get; private set; } = null!;
    public string RequesterNameAtSubmission { get; private set; } = null!;

    /// <summary>
    /// The department's active cumulative-cap cadence at submission. Snapshotted
    /// so audit display is stable even if the department's config changes later.
    /// <see cref="BudgetPeriodType.None"/> when the department had no cumulative
    /// cap at submission (or for rows that pre-date this feature).
    /// </summary>
    public BudgetPeriodType PeriodTypeAtSubmission { get; private set; }

    /// <summary>
    /// The department's period limit (in MMK) at the moment this request was
    /// submitted. <c>null</c> when no cumulative cap was configured.
    /// </summary>
    public decimal? PeriodLimitAtSubmission { get; private set; }

    /// <summary>
    /// The department's already-spent total (in MMK) for the active period
    /// window of submission, *before* this request was counted. <c>null</c> when
    /// no cumulative cap was configured at submission.
    /// </summary>
    public decimal? PeriodSpendBeforeAtSubmission { get; private set; }

    /// <summary>Inclusive UTC start of the active period window at submission. Null when no cap.</summary>
    public DateTime? PeriodStartAtSubmission { get; private set; }

    /// <summary>Exclusive UTC end of the active period window at submission. Null when no cap.</summary>
    public DateTime? PeriodEndAtSubmission { get; private set; }

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

    /// <summary>
    /// Optional budget category (e.g. "Asset" / "Expense") chosen by the
    /// requester at draft time and editable by the assigned department head
    /// while the request is in <see cref="RequestStatus.PendingDeptHead"/>.
    /// Nullable by design — classification is never required. Purely a label:
    /// no routing, finance, or chart-of-account logic depends on it. References
    /// the <see cref="BudgetCategory"/> master list by surrogate FK so an admin
    /// can rename a category without rewriting historical request rows.
    /// </summary>
    public Guid? BudgetCategoryId { get; private set; }

    // === Advance withdrawal — reconciliation phase ===
    // All five fields below stay at their defaults for non-advance requests.

    /// <summary>
    /// Deadline by which the requester must reconcile an advance. Set by Finance
    /// at approval time (required for advances, forbidden otherwise — see
    /// <see cref="ApproveBy"/>) and extendable while reconciliation is pending.
    /// </summary>
    public DateTime? ReconciliationDeadline    { get; private set; }

    /// <summary>When the requester submitted their reconciliation.</summary>
    public DateTime? ReconciliationSubmittedAt { get; private set; }

    /// <summary>
    /// Amount the requester must refund — set when reconciliation lands in
    /// <see cref="RequestStatus.AwaitingRefund"/> (recorded usage &lt; advance).
    /// Zero in every other case.
    /// </summary>
    public decimal   RefundAmount              { get; private set; }

    public DateTime? RefundReceivedAt          { get; private set; }
    public Guid?     RefundReceivedByUserId    { get; private set; }

    // === Child collections (part of the aggregate) ===
    private readonly List<ApprovalAction> _approvalActions = new();
    public IReadOnlyList<ApprovalAction> ApprovalActions => _approvalActions.AsReadOnly();

    private readonly List<Payment> _payments = new();
    public IReadOnlyList<Payment> Payments => _payments.AsReadOnly();

    private readonly List<Attachment> _attachments = new();
    public IReadOnlyList<Attachment> Attachments => _attachments.AsReadOnly();

    private readonly List<AdvanceUsage> _advanceUsages = new();
    public IReadOnlyList<AdvanceUsage> AdvanceUsages => _advanceUsages.AsReadOnly();

    // === Computed helpers ===
    public decimal TotalPaid => _payments.Sum(p => p.Amount);
    public decimal RemainingBalance => ApprovedAmount - TotalPaid;

    /// <summary>Total usage self-reported so far against an advance.</summary>
    public decimal TotalUsageRecorded => _advanceUsages.Sum(u => u.Amount);

    public bool IsTerminal => Status is RequestStatus.Paid
                                    or RequestStatus.Rejected
                                    or RequestStatus.Cancelled
                                    or RequestStatus.Reconciled;

    // For EF Core
    private BudgetRequest() { }
}
