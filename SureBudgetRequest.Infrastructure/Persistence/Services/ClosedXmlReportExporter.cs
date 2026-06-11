using ClosedXML.Excel;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Queries.ExportBudgetRequests;

namespace SureBudgetRequest.Infrastructure.Persistence.Services;

/// <summary>
/// <see cref="IReportExporter"/> backed by ClosedXML. Pure formatter: it relies
/// on the export handler having already denormalized every value (names, status
/// labels). Builds a single "Budget Requests" worksheet in memory.
/// </summary>
public sealed class ClosedXmlReportExporter : IReportExporter
{
    // Number format for MMK / requested amounts — thousands separators, 2 dp.
    private const string AmountFormat = "#,##0.00";
    private const string DateTimeFormat = "yyyy-mm-dd hh:mm";

    // Display timezone for exported timestamps (Myanmar, UTC+6:30, no DST).
    private static readonly TimeSpan MyanmarOffset = new(6, 30, 0);

    public byte[] ExportBudgetRequests(IReadOnlyList<BudgetRequestExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Budget Requests");

        // ── Header row ────────────────────────────────────────────────────────
        string[] headers =
        {
            "Reference",
            "Submitted",
            "Requester",
            "Department",
            "Reason",
            "Currency",
            "Requested Amount",
            "Amount (MMK at submission)",
            "Status",
            "Over-limit",
            "COA Code",
            "COA Name",
            "Withdraw Method",
        };

        for (var col = 0; col < headers.Length; col++)
            ws.Cell(1, col + 1).Value = headers[col];

        ws.Row(1).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);

        // ── Data rows ─────────────────────────────────────────────────────────
        var rowIndex = 2;
        foreach (var r in rows)
        {
            ws.Cell(rowIndex, 1).Value = r.Reference ?? string.Empty;

            var submittedCell = ws.Cell(rowIndex, 2);
            if (r.SubmittedAt.HasValue)
            {
                // Stored as UTC; convert with the fixed Myanmar offset (+6:30,
                // no DST) so exports don't depend on the server OS timezone.
                submittedCell.Value = r.SubmittedAt.Value.Add(MyanmarOffset);
                submittedCell.Style.DateFormat.Format = DateTimeFormat;
            }

            ws.Cell(rowIndex, 3).Value = r.RequesterName;
            ws.Cell(rowIndex, 4).Value = r.DepartmentName;
            ws.Cell(rowIndex, 5).Value = r.Reason;
            ws.Cell(rowIndex, 6).Value = r.CurrencyCode;

            var requestedCell = ws.Cell(rowIndex, 7);
            requestedCell.Value = r.RequestedAmount;
            requestedCell.Style.NumberFormat.Format = AmountFormat;

            var mmkCell = ws.Cell(rowIndex, 8);
            mmkCell.Value = r.AmountInMmkAtSubmission;
            mmkCell.Style.NumberFormat.Format = AmountFormat;

            ws.Cell(rowIndex, 9).Value = r.StatusLabel;
            ws.Cell(rowIndex, 10).Value = r.IsOverLimit ? "Yes" : string.Empty;
            ws.Cell(rowIndex, 11).Value = r.CoaCode ?? string.Empty;
            ws.Cell(rowIndex, 12).Value = r.CoaName ?? string.Empty;
            ws.Cell(rowIndex, 13).Value = r.WithdrawMethodName ?? string.Empty;

            rowIndex++;
        }

        // ── Totals row ────────────────────────────────────────────────────────
        // Mirrors the on-screen footer: sum of MMK-at-submission amounts.
        ws.Cell(rowIndex, 1).Value = "Total";
        ws.Cell(rowIndex, 1).Style.Font.Bold = true;

        var totalCell = ws.Cell(rowIndex, 8);
        totalCell.Value = rows.Sum(r => r.AmountInMmkAtSubmission);
        totalCell.Style.NumberFormat.Format = AmountFormat;
        totalCell.Style.Font.Bold = true;

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
