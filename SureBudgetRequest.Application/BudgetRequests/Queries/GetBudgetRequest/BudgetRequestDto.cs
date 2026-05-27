using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.GetBudgetRequest;

// Note: AttachmentDto + BudgetRequestDto carry AttachmentCategory and
// WithdrawMethodRequiresAttachment so the Web layer can split attachments
// into two sections (Banking info vs Supporting documents) and warn the user
// before submission when the chosen method needs a banking file.

public sealed record BudgetRequestDto(
    Guid Id,
    Guid RequesterId,
    string? Reference,
    DateTime RequestDate,
    BudgetRequestType Type,
    decimal RequestedAmount,
    string CurrencyCode,
    string Reasons,
    string WithdrawerName,
    string WithdrawerJobTitle,
    bool AllowsPartialPayment,
    string? PartialPaymentDetail,
    string? MonthlyOverrunJustification,
    decimal? ManualExchangeRate,
    RequestStatus Status,
    decimal ApprovedAmount,
    decimal TotalPaid,
    decimal RemainingBalance,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? FinalizedAt,
    // Submission snapshots
    Guid DepartmentIdAtSubmission,
    decimal DepartmentLimitAtSubmission,
    decimal? MonthlyLimitAtSubmission,
    decimal? MonthlySpendBeforeAtSubmission,
    decimal ExchangeRateAtSubmission,
    decimal RequestedAmountInMmkAtSubmission,
    Guid DeptHeadIdAtSubmission,
    // Chart of Account — null until Finance approves; display fields resolved
    // from the Coa entity by the query handler.
    Guid? CoaId,
    string? CoaCode,
    string? CoaName,
    // Withdraw method — set by requester at draft time. Null only for rows that
    // pre-date the feature; resolved name + requires-attachment flag come from
    // the WithdrawMethod entity.
    Guid? WithdrawMethodId,
    string? WithdrawMethodName,
    bool WithdrawMethodRequiresAttachment,
    // Child collections
    IReadOnlyList<ApprovalActionDto> ApprovalActions,
    IReadOnlyList<PaymentDto> Payments,
    IReadOnlyList<AttachmentDto> Attachments,
    // ── Advance withdrawal — reconciliation phase ───────────────────────────
    IReadOnlyList<AdvanceUsageDto> AdvanceUsages,
    DateTime? ReconciliationDeadline = null,
    DateTime? ReconciliationSubmittedAt = null,
    decimal RefundAmount = 0m,
    DateTime? RefundReceivedAt = null)
{
    /// <summary>Total advance usage self-reported so far.</summary>
    public decimal TotalUsageRecorded => AdvanceUsages.Sum(u => u.Amount);

    /// <summary>Advance still unaccounted for (= ApprovedAmount − recorded usage).</summary>
    public decimal RemainingAdvance => ApprovedAmount - TotalUsageRecorded;

    /// <summary>
    /// True when an advance is still in the reconciliation phase and its
    /// deadline has already passed. Evaluated against the current UTC time.
    /// </summary>
    public bool IsOverdue =>
        Status == RequestStatus.PendingReconciliation
        && ReconciliationDeadline.HasValue
        && ReconciliationDeadline.Value < DateTime.UtcNow;

    /// <summary>
    /// Construct from the aggregate, optionally resolving the assigned Coa and
    /// WithdrawMethod for display. Pass each entity when the corresponding
    /// FK on <paramref name="e"/> is set; pass null otherwise and the
    /// related display fields will be null too.
    /// </summary>
    public static BudgetRequestDto FromEntity(
        BudgetRequest e,
        Coa? coa = null,
        WithdrawMethod? withdrawMethod = null) => new(
        e.Id,
        e.RequesterId,
        e.Reference,
        e.RequestDate,
        e.Type,
        e.RequestedAmount,
        e.CurrencyCode,
        e.Reasons,
        e.WithdrawerName,
        e.WithdrawerJobTitle,
        e.AllowsPartialPayment,
        e.PartialPaymentDetail,
        e.MonthlyOverrunJustification,
        e.ManualExchangeRate,
        e.Status,
        e.ApprovedAmount,
        e.TotalPaid,
        e.RemainingBalance,
        e.CreatedAt,
        e.SubmittedAt,
        e.FinalizedAt,
        e.DepartmentIdAtSubmission,
        e.DepartmentLimitAtSubmission,
        e.MonthlyLimitAtSubmission,
        e.MonthlySpendBeforeAtSubmission,
        e.ExchangeRateAtSubmission,
        e.RequestedAmountInMmkAtSubmission,
        e.DeptHeadIdAtSubmission,
        e.CoaId,
        coa?.Code,
        coa?.Name,
        e.WithdrawMethodId,
        withdrawMethod?.Name,
        withdrawMethod?.RequiresAttachment ?? false,
        e.ApprovalActions.Select(ApprovalActionDto.FromEntity).ToList(),
        e.Payments.Select(PaymentDto.FromEntity).ToList(),
        e.Attachments.Select(AttachmentDto.FromEntity).ToList(),
        e.AdvanceUsages.Select(AdvanceUsageDto.FromEntity).ToList(),
        e.ReconciliationDeadline,
        e.ReconciliationSubmittedAt,
        e.RefundAmount,
        e.RefundReceivedAt);
}

public sealed record AdvanceUsageDto(
    Guid Id,
    DateTime SpentOn,
    decimal Amount,
    string Description,
    Guid? AttachmentId,
    DateTime RecordedAt)
{
    public static AdvanceUsageDto FromEntity(AdvanceUsage e) => new(
        e.Id, e.SpentOn, e.Amount, e.Description, e.AttachmentId, e.RecordedAt);
}

public sealed record ApprovalActionDto(
    Guid Id,
    ApprovalStage Stage,
    ApprovalDecision Decision,
    Guid ApproverId,
    string? Comment,
    DateTime ActionedAt)
{
    public static ApprovalActionDto FromEntity(ApprovalAction e) => new(
        e.Id, e.Stage, e.Decision, e.ApproverId, e.Comment, e.ActionedAt);
}

public sealed record PaymentDto(
    Guid Id,
    decimal Amount,
    DateTime PaidAt,
    Guid RecordedByUserId,
    string? Reference,
    string? Note,
    Guid? AttachmentId)
{
    public static PaymentDto FromEntity(Payment e) => new(
        e.Id, e.Amount, e.PaidAt, e.RecordedByUserId, e.Reference, e.Note, e.AttachmentId);
}

public sealed record AttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    Guid UploadedByUserId,
    DateTime UploadedAt,
    AttachmentCategory Category)
{
    public static AttachmentDto FromEntity(Attachment e) => new(
        e.Id, e.FileName, e.ContentType, e.SizeBytes, e.UploadedByUserId, e.UploadedAt, e.Category);
}
