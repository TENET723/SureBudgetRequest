using SureBudgetRequest.Application.Abstractions.Services;

namespace SureBudgetRequest.Infrastructure.Time;

/// <summary>
/// Production <see cref="IDateTimeProvider"/> backed by the system clock. The
/// business timezone is Singapore (UTC+8, no DST), matching the advance
/// submission/blackout flow which uses the same fixed offset.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public static readonly TimeSpan SingaporeOffset = TimeSpan.FromHours(8);

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime BusinessNow =>
        DateTime.SpecifyKind(DateTime.UtcNow.Add(SingaporeOffset), DateTimeKind.Unspecified);

    public TimeSpan BusinessUtcOffset => SingaporeOffset;
}
