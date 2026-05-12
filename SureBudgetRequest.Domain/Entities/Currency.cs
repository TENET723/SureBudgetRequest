namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// Currency master record. The natural key is <see cref="Code"/> (e.g. "USD", "MMK").
/// MMK MUST exist as a row with <see cref="RateToMmk"/> = 1 — it is the system's base currency.
/// </summary>
public class Currency
{
    public string Code { get; private set; } = null!;       // "USD", "MMK", "SGD" — PK
    public string Name { get; private set; } = null!;       // "US Dollar"
    public decimal RateToMmk { get; private set; }          // 1 unit of Code = RateToMmk MMK
    public bool IsActive { get; private set; }
    public DateTime RateUpdatedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // For EF Core
    private Currency() { }

    public Currency(string code, string name, decimal rateToMmk)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Currency code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Currency name is required.", nameof(name));
        if (rateToMmk <= 0)
            throw new ArgumentException("Rate must be greater than zero.", nameof(rateToMmk));

        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        RateToMmk = rateToMmk;
        IsActive = true;
        var now = DateTime.UtcNow;
        CreatedAt = now;
        RateUpdatedAt = now;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Currency name is required.", nameof(newName));
        Name = newName.Trim();
    }

    /// <summary>
    /// Update the exchange rate. Returns the previous rate so the caller can write
    /// an audit row (<see cref="CurrencyRateChange"/>) in the same transaction.
    /// MMK is locked at rate 1 and cannot be changed.
    /// </summary>
    public decimal UpdateRate(decimal newRate)
    {
        if (Code == "MMK")
            throw new InvalidOperationException("MMK rate is fixed at 1 and cannot be changed.");
        if (newRate <= 0)
            throw new ArgumentException("Rate must be greater than zero.", nameof(newRate));

        var previous = RateToMmk;
        RateToMmk = newRate;
        RateUpdatedAt = DateTime.UtcNow;
        return previous;
    }

    public void Deactivate()
    {
        if (Code == "MMK")
            throw new InvalidOperationException("MMK cannot be deactivated.");
        IsActive = false;
    }

    public void Reactivate() => IsActive = true;
}
