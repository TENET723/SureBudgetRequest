using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.Abstractions.Services;

/// <summary>
/// Resolves the company-wide financial year and period windows. Reads the
/// configurable start month from the "FinancialYear.StartMonth" app setting and
/// computes boundaries in the business timezone (via <see cref="IDateTimeProvider"/>
/// and <see cref="Domain.Common.FinancialYearCalendar"/>), returning UTC instants
/// suitable for querying <c>SubmittedAt</c> (which is stored in UTC).
/// </summary>
public interface IFinancialYearProvider
{
    /// <summary>
    /// The financial year (identified by its starting calendar year) that the
    /// current business date falls in.
    /// </summary>
    Task<int> GetCurrentFinancialYearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The active period window for the given cadence at the current business date,
    /// returned as a half-open UTC interval <c>[StartUtc, EndUtc)</c>. Throws for
    /// <see cref="BudgetPeriodType.None"/> — callers must guard.
    /// </summary>
    Task<(DateTime StartUtc, DateTime EndUtc)> GetPeriodWindowUtcAsync(
        BudgetPeriodType periodType,
        CancellationToken cancellationToken = default);
}
