namespace SureBudgetRequest.Web.Services;

public sealed record DebitVoucherApprovalStep(
    string StageName,
    string DecisionLabel,
    string ApproverText,
    string ActionedAtText,
    string? Comment = null,
    bool IsAccounting = false);

public sealed record DebitVoucherPrintModel(
    string ReferenceNumber,
    string RequesterName,
    string Reason,
    DateTime? SubmitDate,
    DateTime PrintDate,
    decimal Amount,
    string CurrencyCode,
    string? ApproverName = null,
    string? ApproverRoleLabel = null,
    string? Watermark = null,
    string? AccountingCategoryName = null,
    string? AccountingUserName = null,
    string? PayerName = null,
    IReadOnlyList<DebitVoucherApprovalStep>? ApprovalSteps = null);

public sealed class DebitVoucherPrintService
{
    public DebitVoucherPrintModel? CurrentVoucher { get; private set; }
    public event Action? OnPrintRequested;

    public void RequestPrint(DebitVoucherPrintModel voucher)
    {
        CurrentVoucher = voucher;
        OnPrintRequested?.Invoke();
    }
}
