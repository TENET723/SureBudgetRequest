namespace SureBudgetRequest.Domain.Common;

/// <summary>
/// Pure static helper that computes the current calendar-month window as a
/// half-open UTC interval <c>[StartUtc, EndUtc)</c>. No I/O, no clock, no timezone
/// awareness — the caller passes in the business-local "now" and the fixed offset
/// of the business timezone, mirroring the offset math the old FinancialYearProvider
/// used. Fully unit-testable.
/// </summary>
public static class MonthlyBudgetWindow
{
    /// <summary>
    /// The current calendar-month window for <paramref name="businessNow"/>, shifted
    /// from business-local civil dates back to UTC instants (so it lines up with
    /// <c>SubmittedAt</c>, which is stored in UTC).
    /// </summary>
    public static (DateTime StartUtc, DateTime EndUtc) CurrentUtc(
        DateTime businessNow, TimeSpan businessUtcOffset)
    {
        var localStart = new DateTime(
            businessNow.Year, businessNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var localEnd = localStart.AddMonths(1);

        var startUtc = DateTime.SpecifyKind(localStart - businessUtcOffset, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(localEnd - businessUtcOffset, DateTimeKind.Utc);
        return (startUtc, endUtc);
    }
}
