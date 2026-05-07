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

    // === Requester input (filled at submission, editable in Draft/SentBack) ===
    public Guid RequesterId { get; private set; }
    public DateTime RequestDate { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public string Reasons { get; private set; } = null!;
    public string WithdrawerName { get; private set; } = null!;
    public string WithdrawerJobTitle { get; private set; } = null!;
    public bool AllowsPartialPayment { get; private set; }
    public string? PartialPaymentDetail { get; private set; }

    // === Snapshots taken at submission (for stable audit/routing) ===
    public Guid DepartmentIdAtSubmission { get; private set; }
    public decimal DepartmentLimitAtSubmission { get; private set; }
    public Guid DeptHeadIdAtSubmission { get; private set; }
    public string DeptHeadNameAtSubmission { get; private set; } = null!;
    public Guid? BossIdAtSubmission { get; private set; }           // null if amount <= limit
    public string? BossNameAtSubmission { get; private set; }       // null if amount <= limit
    public string RequesterNameAtSubmission { get; private set; } = null!;

    // === Workflow state ===
    public RequestStatus Status { get; private set; }
    public decimal ApprovedAmount { get; private set; }     // 0 until Finance approves; equals RequestedAmount in v1
    public DateTime CreatedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public DateTime? FinalizedAt { get; private set; }      // when Paid / Rejected / Cancelled

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
