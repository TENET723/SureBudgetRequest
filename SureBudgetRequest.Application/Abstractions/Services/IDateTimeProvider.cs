namespace SureBudgetRequest.Application.Abstractions.Services;

/// <summary>
/// Abstraction over the system clock. The business timezone is Singapore (UTC+8,
/// no DST) — the same zone the advance blackout/submission flow uses. Injecting
/// this (rather than calling <see cref="System.DateTime.UtcNow"/> directly) keeps
/// time-dependent logic — financial-year boundaries in particular — testable.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>The current instant in UTC (Kind = Utc).</summary>
    DateTime UtcNow { get; }

    /// <summary>The current civil time in the business timezone (Kind = Unspecified).</summary>
    DateTime BusinessNow { get; }

    /// <summary>Fixed offset of the business timezone from UTC (Singapore = +08:00).</summary>
    TimeSpan BusinessUtcOffset { get; }
}
