using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.GetBudgetRequest;

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
    // Child collections
    IReadOnlyList<ApprovalActionDto> ApprovalActions,
    IReadOnlyList<PaymentDto> Payments,
    IReadOnlyList<AttachmentDto> Attachments)
{
    public static BudgetRequestDto FromEntity(BudgetRequest e) => new(
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
        e.ApprovalActions.Select(ApprovalActionDto.FromEntity).ToList(),
        e.Payments.Select(PaymentDto.FromEntity).ToList(),
        e.Attachments.Select(AttachmentDto.FromEntity).ToList());
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
    string? Note)
{
    public static PaymentDto FromEntity(Payment e) => new(
        e.Id, e.Amount, e.PaidAt, e.RecordedByUserId, e.Reference, e.Note);
}

public sealed record AttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    Guid UploadedByUserId,
    DateTime UploadedAt)
{
    public static AttachmentDto FromEntity(Attachment e) => new(
        e.Id, e.FileName, e.ContentType, e.SizeBytes, e.UploadedByUserId, e.UploadedAt);
}
