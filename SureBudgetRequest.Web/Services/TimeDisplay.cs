namespace SureBudgetRequest.Web.Services;

/// <summary>
/// Converts stored UTC timestamps to Myanmar time (UTC+6:30, no DST) for
/// display. Uses a fixed offset instead of <see cref="DateTime.ToLocalTime"/>
/// so rendered times never depend on the server OS timezone configuration.
///
/// Applies ONLY to timestamps (SubmittedAt, ActionedAt, PaidAt, …). Calendar
/// dates the user picked (RequestDate, ReconciliationDeadline, SpentOn) are
/// timezone-less and must NOT be converted.
/// </summary>
public static class TimeDisplay
{
    public static readonly TimeSpan MyanmarOffset = new(6, 30, 0);

    /// <summary>Standard timestamp format across all budget-request pages.</summary>
    public const string Format = "dd MMM yyyy HH:mm";

    public static DateTime ToMyanmar(DateTime utc) =>
        DateTime.SpecifyKind(utc.Add(MyanmarOffset), DateTimeKind.Unspecified);

    /// <summary>Myanmar-time string in the standard format, or "—" when null.</summary>
    public static string Mm(DateTime? utc, string format = Format) =>
        utc.HasValue ? ToMyanmar(utc.Value).ToString(format) : "—";

    public static string Mm(DateTime utc, string format = Format) =>
        ToMyanmar(utc).ToString(format);
}
