namespace SureBudgetRequest.Application.BudgetRequests.Queries.ExportBudgetRequests;

/// <summary>
/// Stable identifiers for every exportable column. The enum name is what travels
/// in the export URL's repeated <c>columns</c> query parameter, so renaming a
/// member is a breaking change for any saved/shared export links — add new
/// members, don't rename existing ones.
/// </summary>
public enum ExportColumn
{
    Reference,
    Type,
    RequestDate,
    Submitted,
    Requester,
    Department,
    Reason,
    Currency,
    RequestedAmount,
    AmountMmk,
    Status,
    OverLimit,
    CoaCode,
    CoaName,
    WithdrawMethod,
    ReconciliationDeadline,
    RefundAmount,
    ReimbursementAmount,
    ActualSpent,
    ExternalReferences
}

/// <summary>
/// How the exporter should render a column's value. Keeps formatting decisions
/// (number formats, timezone handling) in the Infrastructure formatter while the
/// Application layer stays library-agnostic.
/// </summary>
public enum ExportValueKind
{
    Text,
    Amount,
    /// <summary>UTC timestamp, shown in office-local time with a date+time format.</summary>
    DateTime,
    /// <summary>Calendar date, rendered as-stored (no timezone shift) with a date-only format.</summary>
    Date,
    /// <summary>Boolean rendered as "Yes" / blank.</summary>
    YesNo
}

/// <summary>
/// A single column definition: its stable key, header text, how to render it,
/// how to pull its value from a row, and whether it participates in the totals
/// row. This is the one place column metadata lives — the export handler, the
/// exporter, and the UI's column picker all read from here.
/// </summary>
public sealed record ExportColumnSpec(
    ExportColumn Key,
    string Header,
    ExportValueKind Kind,
    Func<BudgetRequestExportRow, object?> Accessor,
    bool IsSummable = false);

/// <summary>
/// The canonical column registry. <see cref="DefaultOrder"/> is both the full
/// set of selectable columns and the order used when no explicit selection is
/// supplied (backward-compatible with export links that predate column
/// customization).
/// </summary>
public static class ExportColumns
{
    public static readonly IReadOnlyList<ExportColumnSpec> DefaultOrder = new[]
    {
        new ExportColumnSpec(ExportColumn.Reference,              "Reference",                   ExportValueKind.Text,     r => r.Reference),
        new ExportColumnSpec(ExportColumn.Type,                  "Type",                        ExportValueKind.Text,     r => r.TypeLabel),
        new ExportColumnSpec(ExportColumn.RequestDate,           "Request Date",                ExportValueKind.Date,     r => r.RequestDate),
        new ExportColumnSpec(ExportColumn.Submitted,             "Submitted",                   ExportValueKind.DateTime, r => r.SubmittedAt),
        new ExportColumnSpec(ExportColumn.Requester,             "Requester",                   ExportValueKind.Text,     r => r.RequesterName),
        new ExportColumnSpec(ExportColumn.Department,            "Department",                  ExportValueKind.Text,     r => r.DepartmentName),
        new ExportColumnSpec(ExportColumn.Reason,                "Reason",                      ExportValueKind.Text,     r => r.Reason),
        new ExportColumnSpec(ExportColumn.Currency,              "Currency",                    ExportValueKind.Text,     r => r.CurrencyCode),
        // Requested Amount is intentionally NOT summable — rows may be in
        // different currencies, so a raw sum would be a meaningless number.
        new ExportColumnSpec(ExportColumn.RequestedAmount,       "Requested Amount",            ExportValueKind.Amount,   r => r.RequestedAmount),
        new ExportColumnSpec(ExportColumn.AmountMmk,             "Amount (MMK at submission)",  ExportValueKind.Amount,   r => r.AmountInMmkAtSubmission, IsSummable: true),
        new ExportColumnSpec(ExportColumn.Status,                "Status",                      ExportValueKind.Text,     r => r.StatusLabel),
        new ExportColumnSpec(ExportColumn.OverLimit,             "Over-limit",                  ExportValueKind.YesNo,    r => r.IsOverLimit),
        new ExportColumnSpec(ExportColumn.CoaCode,               "COA Code",                    ExportValueKind.Text,     r => r.CoaCode),
        new ExportColumnSpec(ExportColumn.CoaName,               "COA Name",                    ExportValueKind.Text,     r => r.CoaName),
        new ExportColumnSpec(ExportColumn.WithdrawMethod,        "Withdraw Method",             ExportValueKind.Text,     r => r.WithdrawMethodName),
        new ExportColumnSpec(ExportColumn.ReconciliationDeadline,"Reconciliation Deadline",     ExportValueKind.Date,     r => r.ReconciliationDeadline),
        // Advance settlement amounts, in the request's own currency. Not summable
        // — rows may mix currencies (same reason as Requested Amount).
        new ExportColumnSpec(ExportColumn.RefundAmount,          "Refund Amount",               ExportValueKind.Amount,   r => r.RefundAmount),
        new ExportColumnSpec(ExportColumn.ReimbursementAmount,   "Reimbursement Amount",        ExportValueKind.Amount,   r => r.ReimbursementAmount),
        // Actual spent on a settled advance (request's own currency). Blank for
        // non-advances / unreconciled advances. Not summable (mixed currencies).
        new ExportColumnSpec(ExportColumn.ActualSpent,           "Actual Spent",                ExportValueKind.Amount,   r => r.ActualSpent),
        new ExportColumnSpec(ExportColumn.ExternalReferences,    "External References",         ExportValueKind.Text,     r => r.ExternalReferences),
    };

    private static readonly IReadOnlyDictionary<ExportColumn, ExportColumnSpec> ByKey =
        DefaultOrder.ToDictionary(c => c.Key);

    /// <summary>
    /// Resolves the ordered list of column specs to export. A null or empty
    /// selection means "all columns, default order". Unknown keys are ignored and
    /// duplicates are collapsed to their first occurrence, preserving the caller's
    /// order. If the selection resolves to nothing usable, falls back to the full
    /// default set so an export never comes out empty.
    /// </summary>
    public static IReadOnlyList<ExportColumnSpec> Resolve(IReadOnlyCollection<ExportColumn>? selected)
    {
        if (selected is null || selected.Count == 0)
            return DefaultOrder;

        var result = new List<ExportColumnSpec>(selected.Count);
        var seen = new HashSet<ExportColumn>();
        foreach (var key in selected)
            if (seen.Add(key) && ByKey.TryGetValue(key, out var spec))
                result.Add(spec);

        return result.Count > 0 ? result : DefaultOrder;
    }
}
