using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class AppSettingTests
{
    // =========================================================================
    // 1. Constructor Tests
    // =========================================================================

    [Theory]
    [InlineData("MaxAdvanceLimit", "5000000", "Maximum advance allowed per request")]
    [InlineData("DefaultCurrency", "MMK", null)]
    [InlineData("SlackEnabled", "true", "")]
    public void Constructor_ValidParameters_ShouldInitializeProperties(
        string key, string value, string? description)
    {
        // Act
        var setting = new AppSetting(key, value, description);

        // Assert
        setting.Key.Should().Be(key);
        setting.Value.Should().Be(value);
        setting.Description.Should().Be(description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyOrWhitespaceKey_ShouldThrowArgumentException(string? invalidKey)
    {
        // Act
        var act = () => new AppSetting(invalidKey!, "val", "desc");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("key")
            .WithMessage("*Key is required.*");
    }

    // =========================================================================
    // 2. State Mutation Tests
    // =========================================================================

    [Theory]
    [InlineData("NewValue123")]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateValue_ShouldUpdateValueProperty(string newValue)
    {
        // Arrange
        var setting = new AppSetting("Theme", "Light");

        // Act
        setting.UpdateValue(newValue);

        // Assert
        setting.Value.Should().Be(newValue);
    }

    [Theory]
    [InlineData("Updated description text")]
    [InlineData(null)]
    [InlineData("")]
    public void UpdateDescription_ShouldUpdateDescriptionProperty(string? newDescription)
    {
        // Arrange
        var setting = new AppSetting("Theme", "Light", "Original description");

        // Act
        setting.UpdateDescription(newDescription);

        // Assert
        setting.Description.Should().Be(newDescription);
    }
}
