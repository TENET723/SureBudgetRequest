namespace SureBudgetRequest.Domain.Enums;

/// <summary>
/// Cadence of a department's cumulative spending cap. The window is anchored to
/// the company-wide financial year (see <see cref="Common.FinancialYearCalendar"/>).
///
/// <list type="bullet">
///   <item><c>None</c> — no cumulative cap from this financial year onward.</item>
///   <item><c>Monthly</c> — calendar months bucketed within the financial year.</item>
///   <item><c>Quarterly</c> — the financial year split into four equal quarters.</item>
///   <item><c>Yearly</c> — the whole financial year as a single window.</item>
/// </list>
///
/// Stored as <c>int</c>. Order is stable — append new members, never reorder.
/// </summary>
public enum BudgetPeriodType
{
    None = 0,
    Monthly = 1,
    Quarterly = 2,
    Yearly = 3
}
