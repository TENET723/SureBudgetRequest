namespace SureBudgetRequest.Domain.Enums;

/// <summary>
/// Discriminates what kind of supporting file a request attachment is.
///
/// <list type="bullet">
/// <item><b>General</b> — receipts, quotes, contracts. The default for any
/// attachment uploaded without a specific category, including all rows that
/// pre-date the category feature.</item>
/// <item><b>WithdrawMethod</b> — banking-info evidence (e.g. bank book photo)
/// required when the requester's chosen <c>WithdrawMethod</c> has
/// <c>RequiresAttachment = true</c>. Validated at submit time.</item>
/// <item><b>UsageReceipt</b> — a receipt backing a single advance-usage line
/// item recorded during the reconciliation phase. Linked from
/// <c>AdvanceUsage.AttachmentId</c>.</item>
/// </list>
/// </summary>
public enum AttachmentCategory
{
    General = 0,
    WithdrawMethod = 1,
    UsageReceipt = 2,
    PaymentReceipt = 3
}
