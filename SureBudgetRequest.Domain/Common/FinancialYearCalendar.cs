using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Common;

/// <summary>
/// Pure value object that does all financial-year and period-window date math.
/// Built from a single company-wide start month (1-12). No I/O, no clock, no
/// timezone awareness — callers pass in whatever <see cref="DateTime"/> they want
/// the math performed against (the infrastructure provider is responsible for
/// converting between UTC and the business timezone before/after calling here),
/// which keeps this class fully unit-testable.
///
/// A financial year is identified by the calendar year in which it STARTS.
/// With <see cref="StartMonth"/> = 4 (April), the financial year 2026 runs from
/// 2026-04-01 (inclusive) to 2027-04-01 (exclusive). With StartMonth = 1 it is
/// identical to the calendar year.
///
/// All windows returned are half-open: <c>[Start, End)</c>. The <see cref="DateTime.Kind"/>
/// of returned values is <see cref="DateTimeKind.Unspecified"/> — they are civil
/// dates, not instants; the caller stamps the kind it needs.
/// </summary>
public sealed class FinancialYearCalendar
{
    public int StartMonth { get; }

    public FinancialYearCalendar(int startMonth)
    {
        if (startMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(
                nameof(startMonth), startMonth, "Financial-year start month must be between 1 and 12.");
        StartMonth = startMonth;
    }

    /// <summary>
    /// Returns the financial year (identified by its starting calendar year) that
    /// the supplied date falls in.
    /// </summary>
    public int FinancialYearFor(DateTime date)
        => date.Month >= StartMonth ? date.Year : date.Year - 1;

    /// <summary>
    /// The first day of the given financial year (00:00, Kind = Unspecified).
    /// </summary>
    public DateTime FinancialYearStart(int financialYear)
        => new(financialYear, StartMonth, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>
    /// Returns the half-open window <c>[start, end)</c> for the period of the given
    /// cadence that contains <paramref name="date"/>. Throws for
    /// <see cref="BudgetPeriodType.None"/> — callers must guard, since "None" means
    /// there is no window at all.
    /// </summary>
    public (DateTime Start, DateTime End) PeriodWindowFor(DateTime date, BudgetPeriodType periodType)
    {
        switch (periodType)
        {
            case BudgetPeriodType.Yearly:
            {
                var fy = FinancialYearFor(date);
                var start = FinancialYearStart(fy);
                return (start, start.AddYears(1));
            }
            case BudgetPeriodType.Quarterly:
            {
                var fy = FinancialYearFor(date);
                var fyStart = FinancialYearStart(fy);
                // Whole months elapsed since the FY started (0-11), then bucket into
                // one of four 3-month quarters.
                var monthsSinceFyStart = (date.Year - fy) * 12 + (date.Month - StartMonth);
                var quarterIndex = monthsSinceFyStart / 3;   // 0..3
                var start = fyStart.AddMonths(quarterIndex * 3);
                return (start, start.AddMonths(3));
            }
            case BudgetPeriodType.Monthly:
            {
                // Calendar-month bucket. FY anchoring does not move month boundaries.
                var start = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
                return (start, start.AddMonths(1));
            }
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(periodType), periodType,
                    "PeriodWindowFor is undefined for 'None' — there is no cumulative window.");
        }
    }
}
