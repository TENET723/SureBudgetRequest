using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class CurrencyTests
{
    // =========================================================================
    // 1. Constructor & Invariant Normalization
    // =========================================================================

    [Fact]
    public void Constructor_ValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var currency = new Currency("usd", "  US Dollar  ", 3500.50m);
        var after = DateTime.UtcNow;

        // Assert
        currency.Code.Should().Be("USD");
        currency.Name.Should().Be("US Dollar");
        currency.RateToMmk.Should().Be(3500.50m);
        currency.IsActive.Should().BeTrue();
        currency.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        currency.RateUpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Theory]
    [InlineData("mmk", "MMK")]
    [InlineData("  sgd  ", "SGD")]
    [InlineData("thb", "THB")]
    [InlineData("eur", "EUR")]
    [InlineData("jpy", "JPY")]
    public void Constructor_Code_ShouldBeTrimmedAndUpperCased(string rawCode, string expectedCode)
    {
        // Act
        var currency = new Currency(rawCode, "Currency Name", 100m);

        // Assert
        currency.Code.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidCode_ShouldThrowArgumentException(string? invalidCode)
    {
        // Act
        Action act = () => new Currency(invalidCode!, "US Dollar", 3500m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("code")
            .WithMessage("*Currency code is required.*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        // Act
        Action act = () => new Currency("USD", invalidName!, 3500m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Currency name is required.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.0001)]
    [InlineData(-100)]
    [InlineData(-50000)]
    public void Constructor_ZeroOrNegativeRate_ShouldThrowArgumentException(decimal invalidRate)
    {
        // Act
        Action act = () => new Currency("USD", "US Dollar", invalidRate);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("rateToMmk")
            .WithMessage("*Rate must be greater than zero.*");
    }

    // =========================================================================
    // 2. Rename Method Tests
    // =========================================================================

    [Theory]
    [InlineData("Euro")]
    [InlineData("  Euro  ")]
    [InlineData("New Euro Designation")]
    public void Rename_ValidNewName_ShouldUpdateAndTrimName(string newName)
    {
        // Arrange
        var currency = new Currency("EUR", "Old Euro", 3800m);

        // Act
        currency.Rename(newName);

        // Assert
        currency.Name.Should().Be(newName.Trim());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_InvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        // Arrange
        var currency = new Currency("EUR", "Euro", 3800m);

        // Act
        Action act = () => currency.Rename(invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newName")
            .WithMessage("*Currency name is required.*");
    }

    // =========================================================================
    // 3. UpdateRate Tests & Base Currency Invariant
    // =========================================================================

    [Fact]
    public void UpdateRate_NonMmkCurrency_ShouldUpdateRateAndReturnPreviousRate()
    {
        // Arrange
        const decimal initialRate = 3500m;
        const decimal newRate = 3650.75m;
        var currency = new Currency("USD", "US Dollar", initialRate);
        var beforeUpdate = currency.RateUpdatedAt;

        // Act
        var returnedPreviousRate = currency.UpdateRate(newRate);

        // Assert
        returnedPreviousRate.Should().Be(initialRate);
        currency.RateToMmk.Should().Be(newRate);
        currency.RateUpdatedAt.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public void UpdateRate_MmkBaseCurrency_ShouldThrowInvalidOperationException()
    {
        // Arrange: MMK is the system's base currency
        var mmk = new Currency("MMK", "Myanmar Kyat", 1m);

        // Act
        Action act = () => mmk.UpdateRate(1.5m);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("MMK rate is fixed at 1 and cannot be changed.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-1000)]
    public void UpdateRate_ZeroOrNegativeRate_ShouldThrowArgumentException(decimal invalidRate)
    {
        // Arrange
        var currency = new Currency("THB", "Thai Baht", 100m);

        // Act
        Action act = () => currency.UpdateRate(invalidRate);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newRate")
            .WithMessage("*Rate must be greater than zero.*");
    }

    // =========================================================================
    // 4. Deactivate & Reactivate Tests
    // =========================================================================

    [Fact]
    public void Deactivate_NonMmkCurrency_ShouldSetIsActiveToFalse()
    {
        // Arrange
        var currency = new Currency("SGD", "Singapore Dollar", 2600m);
        currency.IsActive.Should().BeTrue();

        // Act
        currency.Deactivate();

        // Assert
        currency.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_MmkBaseCurrency_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var mmk = new Currency("MMK", "Myanmar Kyat", 1m);

        // Act
        Action act = () => mmk.Deactivate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("MMK cannot be deactivated.");
        mmk.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Reactivate_InactiveCurrency_ShouldRestoreIsActiveToTrue()
    {
        // Arrange
        var currency = new Currency("SGD", "Singapore Dollar", 2600m);
        currency.Deactivate();
        currency.IsActive.Should().BeFalse();

        // Act
        currency.Reactivate();

        // Assert
        currency.IsActive.Should().BeTrue();
    }
}
