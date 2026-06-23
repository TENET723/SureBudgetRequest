namespace SureBudgetRequest.Application.BudgetRequests.Queries.ExportBudgetRequests;

/// <summary>
/// A flat, fully-denormalized projection of a budget request — one instance per
/// spreadsheet row. All display resolution (requester/department names, status
/// label, type label) is done by the export handler so the Infrastructure
/// exporter stays a pure formatter. Which fields are written, in what order, is
/// decided by the selected <see cref="ExportColumn"/> list — see
/// <see cref="ExportColumns"/> for the registry that maps each column to its
/// header, value kind, and accessor over this row.
/// </summary>
public sealed record BudgetRequestExportRow(
    string? Reference,
    // Human-friendly request type label (resolved by the handler).
    string TypeLabel,
    // Date-only value (user-entered request date). Rendered without timezone shift.
    DateTime RequestDate,
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
    string? WithdrawMethodName,
    // Date-only deadline (advance reconciliation). Null for non-advances.
    DateTime? ReconciliationDeadline);

/// <summary>
/// A generated file ready to be streamed to the browser by a Web endpoint.
/// Mirrors the role of <c>AttachmentContent</c> for in-memory generated content.
/// </summary>
public sealed record FileDownload(byte[] Content, string FileName, string ContentType);
