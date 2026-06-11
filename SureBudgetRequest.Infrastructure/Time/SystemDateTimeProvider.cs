using SureBudgetRequest.Application.Abstractions.Services;

namespace SureBudgetRequest.Infrastructure.Time;

/// <summary>
/// Production <see cref="IDateTimeProvider"/> backed by the system clock. The
/// business timezone is Myanmar (UTC+6:30, no DST) — aSure's office — so
/// monthly budget windows and the advance submission/blackout flow roll over
/// at Myanmar midnight.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public static readonly TimeSpan MyanmarOffset = new(6, 30, 0);

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime BusinessNow =>
        DateTime.SpecifyKind(DateTime.UtcNow.Add(MyanmarOffset), DateTimeKind.Unspecified);

    public TimeSpan BusinessUtcOffset => MyanmarOffset;
}
