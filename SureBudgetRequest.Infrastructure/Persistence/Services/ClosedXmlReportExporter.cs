using ClosedXML.Excel;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Queries.ExportBudgetRequests;

namespace SureBudgetRequest.Infrastructure.Persistence.Services;

/// <summary>
/// <see cref="IReportExporter"/> backed by ClosedXML. Pure formatter: it relies
/// on the export handler having already denormalized every value (names, status
/// label, type label) and on the supplied <see cref="ExportColumnSpec"/> list to
/// decide which columns to write, in what order. Builds a single "Budget
/// Requests" worksheet in memory.
/// </summary>
public sealed class ClosedXmlReportExporter : IReportExporter
{
    // Number format for MMK / requested amounts — thousands separators, 2 dp.
    private const string AmountFormat = "#,##0.00";
    private const string DateTimeFormat = "yyyy-mm-dd hh:mm";
    private const string DateFormat = "yyyy-mm-dd";

    // Display timezone for exported UTC timestamps (Myanmar, UTC+6:30, no DST).
    private static readonly TimeSpan MyanmarOffset = new(6, 30, 0);

    public byte[] ExportBudgetRequests(
        IReadOnlyList<BudgetRequestExportRow> rows,
        IReadOnlyList<ExportColumnSpec> columns)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Budget Requests");

        // ── Header row ────────────────────────────────────────────────────────
        for (var c = 0; c < columns.Count; c++)
            ws.Cell(1, c + 1).Value = columns[c].Header;

        ws.Row(1).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);

        // ── Data rows ─────────────────────────────────────────────────────────
        var rowIndex = 2;
        foreach (var r in rows)
        {
            for (var c = 0; c < columns.Count; c++)
                WriteCell(ws.Cell(rowIndex, c + 1), columns[c], r);

            rowIndex++;
        }

        // ── Totals row ────────────────────────────────────────────────────────
        // Sum every selected summable column (currently just MMK-at-submission).
        // Omitted entirely when no summable column is in the selection.
        if (columns.Any(col => col.IsSummable))
        {
            ws.Cell(rowIndex, 1).Value = "Total";
            ws.Cell(rowIndex, 1).Style.Font.Bold = true;

            for (var c = 0; c < columns.Count; c++)
            {
                if (!columns[c].IsSummable) continue;

                var total = rows.Sum(r => ToDecimal(columns[c].Accessor(r)));
                var cell = ws.Cell(rowIndex, c + 1);
                cell.Value = total;
                cell.Style.NumberFormat.Format = AmountFormat;
                cell.Style.Font.Bold = true;
            }
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Writes one cell according to the column's value kind. The accessor returns
    /// a boxed value; each kind unboxes to its concrete type so ClosedXML gets a
    /// typed (not stringified) value where it matters — amounts and dates stay
    /// numeric/temporal so Excel can sort and format them.
    /// </summary>
    private static void WriteCell(IXLCell cell, ExportColumnSpec column, BudgetRequestExportRow row)
    {
        var value = column.Accessor(row);

        switch (column.Kind)
        {
            case ExportValueKind.Text:
                cell.Value = value as string ?? string.Empty;
                break;

            case ExportValueKind.Amount:
                cell.Value = ToDecimal(value);
                cell.Style.NumberFormat.Format = AmountFormat;
                break;

            case ExportValueKind.DateTime:
                // Stored as UTC; convert with the fixed Myanmar offset (+6:30, no
                // DST) so exports don't depend on the server OS timezone.
                if (value is DateTime dt)
                {
                    cell.Value = dt.Add(MyanmarOffset);
                    cell.Style.DateFormat.Format = DateTimeFormat;
                }
                break;

            case ExportValueKind.Date:
                // Calendar date — rendered as-stored, no timezone shift, so a date
                // can't slip a day either side of midnight.
                if (value is DateTime d)
                {
                    cell.Value = d.Date;
                    cell.Style.DateFormat.Format = DateFormat;
                }
                break;

            case ExportValueKind.YesNo:
                cell.Value = value is true ? "Yes" : string.Empty;
                break;
        }
    }

    private static decimal ToDecimal(object? value) =>
        value is decimal d ? d : 0m;
}
