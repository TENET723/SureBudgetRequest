namespace SureBudgetRequest.Application.BudgetRequests.Queries.ExportBudgetRequests;

/// <summary>
/// A flat, fully-denormalized projection of a budget request — one instance per
/// spreadsheet row. All display resolution (requester/department names, status
/// label) is done by the export handler so the Infrastructure exporter stays a
/// pure formatter. Field order matches the spreadsheet column order.
/// </summary>
public sealed record BudgetRequestExportRow(
    string? Reference,
    // UTC value; the exporter converts to local (office) time.
    DateTime? SubmittedAt,
    string RequesterName,
    string DepartmentName,
    string Reason,
    string CurrencyCode,
    decimal RequestedAmount,
    decimal AmountInMmkAtSubmission,
    string StatusLabel,
    bool IsOverLimit,
    string? CoaCode,
    string? CoaName,
    string? WithdrawMethodName);

/// <summary>
/// A generated file ready to be streamed to the browser by a Web endpoint.
/// Mirrors the role of <c>AttachmentContent</c> for in-memory generated content.
/// </summary>
public sealed record FileDownload(byte[] Content, string FileName, string ContentType);
