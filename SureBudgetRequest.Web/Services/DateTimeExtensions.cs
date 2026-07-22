namespace SureBudgetRequest.Web.Services;

/// <summary>
/// Extension methods for converting UTC <see cref="DateTime"/> and <see cref="DateTime"/>?
/// to Myanmar Time (UTC+06:30, no DST) for UI display.
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Fixed UTC offset for Myanmar (UTC+06:30).
    /// </summary>
    public static readonly TimeSpan MyanmarOffset = new(6, 30, 0);

    /// <summary>
    /// Standard timestamp format across budget request pages (e.g., "22 Jul 2026 2:02 PM").
    /// </summary>
    public const string DefaultFormat = "dd MMM yyyy h:mm tt";

    /// <summary>
    /// Converts a UTC <see cref="DateTime"/> to Myanmar local <see cref="DateTime"/> (UTC+06:30).
    /// </summary>
    public static DateTime ToMyanmarDateTime(this DateTime utc) =>
        DateTime.SpecifyKind(utc.Add(MyanmarOffset), DateTimeKind.Unspecified);

    /// <summary>
    /// Formats a UTC <see cref="DateTime"/> as a Myanmar local time string with AM/PM (e.g. "22 Jul 2026 2:02 PM").
    /// </summary>
    public static string ToMyanmarTime(this DateTime utc, string format = DefaultFormat) =>
        utc.ToMyanmarDateTime().ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a nullable UTC <see cref="DateTime"/> as a Myanmar local time string with AM/PM, or returns a fallback when null (default "—").
    /// </summary>
    public static string ToMyanmarTime(this DateTime? utc, string format = DefaultFormat, string fallback = "—") =>
        utc.HasValue ? utc.Value.ToMyanmarTime(format) : fallback;

    /// <summary>
    /// Today's calendar date in Myanmar Time.
    /// </summary>
    public static DateTime MyanmarToday => DateTime.UtcNow.ToMyanmarDateTime().Date;
}
