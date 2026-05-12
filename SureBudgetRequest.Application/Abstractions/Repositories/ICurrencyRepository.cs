using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface ICurrencyRepository
{
    Task<Currency?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Currency>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task AddAsync(Currency currency, CancellationToken cancellationToken = default);

    Task AddRateChangeAsync(CurrencyRateChange change, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rate-change audit history, newest first. Optional currency code filter.
    /// </summary>
    Task<IReadOnlyList<CurrencyRateChange>> ListRateChangesAsync(
        string? currencyCode = null,
        int? take = null,
        CancellationToken cancellationToken = default);
}
