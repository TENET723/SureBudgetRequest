namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// Audit row written every time a currency's exchange rate changes.
/// Created by the Application layer in the same transaction as <see cref="Currency.UpdateRate"/>.
/// </summary>
public class CurrencyRateChange
{
    public Guid Id { get; private set; }
    public string CurrencyCode { get; private set; } = null!;
    public decimal OldRate { get; private set; }
    public decimal NewRate { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public DateTime ChangedAt { get; private set; }

    // For EF Core
    private CurrencyRateChange() { }

    public CurrencyRateChange(
        string currencyCode,
        decimal oldRate,
        decimal newRate,
        Guid changedByUserId)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("Currency code is required.", nameof(currencyCode));
        if (oldRate <= 0)
            throw new ArgumentException("Old rate must be greater than zero.", nameof(oldRate));
        if (newRate <= 0)
            throw new ArgumentException("New rate must be greater than zero.", nameof(newRate));

        //Id = Guid.NewGuid();
        CurrencyCode = currencyCode.ToUpperInvariant();
        OldRate = oldRate;
        NewRate = newRate;
        ChangedByUserId = changedByUserId;
        ChangedAt = DateTime.UtcNow;
    }
}
