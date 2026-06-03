using SureBudgetRequest.Application.BudgetRequests.Queries.ExportBudgetRequests;

namespace SureBudgetRequest.Application.Abstractions.Services;

/// <summary>
/// Renders report data into a downloadable file. Keeps the concrete spreadsheet
/// library (ClosedXML) out of the Application and Domain layers — the
/// implementation lives in Infrastructure.
/// </summary>
public interface IReportExporter
{
    /// <summary>
    /// Builds an <c>.xlsx</c> workbook for the supplied, fully-denormalized
    /// budget-request rows and returns the raw bytes. Synchronous because
    /// ClosedXML builds the workbook entirely in memory.
    /// </summary>
    byte[] ExportBudgetRequests(IReadOnlyList<BudgetRequestExportRow> rows);
}
