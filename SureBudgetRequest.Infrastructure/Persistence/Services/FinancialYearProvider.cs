using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Infrastructure.Persistence.Services;

/// <summary>
/// Reads the company-wide financial-year start month from the
/// "FinancialYear.StartMonth" app setting and computes FY/period boundaries in
/// the business timezone, returning UTC instants for querying SubmittedAt.
/// </summary>
public sealed class FinancialYearProvider : IFinancialYearProvider
{
    /// <summary>App-setting key holding the FY start month (1-12).</summary>
    public const string StartMonthSettingKey = "FinancialYear.StartMonth";

    /// <summary>Fallback when the setting is missing or invalid. April — a common
    /// non-calendar financial-year start. Kept in sync with the seeded value.</summary>
    public const int DefaultStartMonth = 4;

    private readonly IAppSettingRepository _appSettingRepository;
    private readonly IDateTimeProvider _clock;

    public FinancialYearProvider(IAppSettingRepository appSettingRepository, IDateTimeProvider clock)
    {
        _appSettingRepository = appSettingRepository;
        _clock = clock;
    }

    public async Task<int> GetCurrentFinancialYearAsync(CancellationToken cancellationToken = default)
    {
        var calendar = await GetCalendarAsync(cancellationToken);
        return calendar.FinancialYearFor(_clock.BusinessNow);
    }

    public async Task<(DateTime StartUtc, DateTime EndUtc)> GetPeriodWindowUtcAsync(
        BudgetPeriodType periodType,
        CancellationToken cancellationToken = default)
    {
        var calendar = await GetCalendarAsync(cancellationToken);

        // Compute the window on business-local civil dates, then shift back to UTC
        // so it lines up with SubmittedAt (stored in UTC).
        var (localStart, localEnd) = calendar.PeriodWindowFor(_clock.BusinessNow, periodType);
        var offset = _clock.BusinessUtcOffset;
        var startUtc = DateTime.SpecifyKind(localStart - offset, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(localEnd - offset, DateTimeKind.Utc);
        return (startUtc, endUtc);
    }

    private async Task<FinancialYearCalendar> GetCalendarAsync(CancellationToken cancellationToken)
    {
        var setting = await _appSettingRepository.GetByKeyAsync(StartMonthSettingKey, cancellationToken);
        var startMonth = DefaultStartMonth;
        if (setting is not null && int.TryParse(setting.Value, out var parsed) && parsed is >= 1 and <= 12)
            startMonth = parsed;
        return new FinancialYearCalendar(startMonth);
    }
}
