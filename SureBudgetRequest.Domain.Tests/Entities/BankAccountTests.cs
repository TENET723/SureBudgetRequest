using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class BankAccountTests
{
    // =========================================================================
    // 1. Constructor & Normalization
    // =========================================================================

    [Fact]
    public void Constructor_ValidParameters_ShouldTrimAndInitialize()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var account = new BankAccount(
            "  KBZ Bank  ",
            "  0123456789012  ",
            "  Company Name Ltd  ");
        var after = DateTime.UtcNow;

        // Assert
        account.BankName.Should().Be("KBZ Bank");
        account.AccountNumber.Should().Be("0123456789012");
        account.AccountHolderName.Should().Be("Company Name Ltd");
        account.IsActive.Should().BeTrue();
        account.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void Constructor_EmptyOrWhitespaceOptionalFields_ShouldStoreAsNull(string? number, string? holder)
    {
        // Act
        var account = new BankAccount("AYA Bank", number, holder);

        // Assert
        account.BankName.Should().Be("AYA Bank");
        account.AccountNumber.Should().BeNull();
        account.AccountHolderName.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidBankName_ShouldThrowArgumentException(string? invalidName)
    {
        // Act
        Action act = () => new BankAccount(invalidName!, "123", "Holder");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("bankName")
            .WithMessage("*Bank name is required.*");
    }

    // =========================================================================
    // 2. Update Method Tests
    // =========================================================================

    [Fact]
    public void Update_ValidParameters_ShouldUpdateAndTrimFields()
    {
        // Arrange
        var account = new BankAccount("Initial Bank", "111", "Initial Holder");

        // Act
        account.Update("  CB Bank  ", "  987654321  ", "  New Account Holder  ");

        // Assert
        account.BankName.Should().Be("CB Bank");
        account.AccountNumber.Should().Be("987654321");
        account.AccountHolderName.Should().Be("New Account Holder");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void Update_ClearingOptionalFields_ShouldSetThemToNull(string? number, string? holder)
    {
        // Arrange
        var account = new BankAccount("KBZ Bank", "12345", "Company Ltd");

        // Act
        account.Update("KBZ Bank", number, holder);

        // Assert
        account.AccountNumber.Should().BeNull();
        account.AccountHolderName.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_InvalidBankName_ShouldThrowArgumentException(string? invalidName)
    {
        // Arrange
        var account = new BankAccount("Valid Bank", "123", "Holder");

        // Act
        Action act = () => account.Update(invalidName!, "123", "Holder");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("bankName")
            .WithMessage("*Bank name is required.*");
    }

    // =========================================================================
    // 3. Deactivate & Reactivate
    // =========================================================================

    [Fact]
    public void DeactivateAndReactivate_ShouldToggleIsActive()
    {
        // Arrange
        var account = new BankAccount("UAB Bank", null, null);
        account.IsActive.Should().BeTrue();

        // Act 1: Deactivate
        account.Deactivate();
        account.IsActive.Should().BeFalse();

        // Act 2: Reactivate
        account.Reactivate();
        account.IsActive.Should().BeTrue();
    }
}
