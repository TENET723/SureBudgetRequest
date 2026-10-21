using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class BudgetCategoryTests
{
    // =========================================================================
    // 1. Constructor & Defaults
    // =========================================================================

    [Fact]
    public void Constructor_ValidName_ShouldTrimAndInitialize()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var category = new BudgetCategory("  Office Asset  ");
        var after = DateTime.UtcNow;

        // Assert
        category.Name.Should().Be("Office Asset");
        category.IsActive.Should().BeTrue();
        category.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        // Act
        Action act = () => new BudgetCategory(invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Name is required.*");
    }

    // =========================================================================
    // 2. Rename Method Tests
    // =========================================================================

    [Theory]
    [InlineData("Operational Expense")]
    [InlineData("  Capex Expense  ")]
    public void Rename_ValidName_ShouldUpdateAndTrimName(string newName)
    {
        // Arrange
        var category = new BudgetCategory("Old Category");

        // Act
        category.Rename(newName);

        // Assert
        category.Name.Should().Be(newName.Trim());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_InvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        // Arrange
        var category = new BudgetCategory("Valid Category");

        // Act
        Action act = () => category.Rename(invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newName")
            .WithMessage("*Name is required.*");
    }

    // =========================================================================
    // 3. Deactivate & Reactivate
    // =========================================================================

    [Fact]
    public void DeactivateAndReactivate_ShouldToggleIsActive()
    {
        // Arrange
        var category = new BudgetCategory("Marketing Asset");
        category.IsActive.Should().BeTrue();

        // Act 1: Deactivate
        category.Deactivate();
        category.IsActive.Should().BeFalse();

        // Act 2: Reactivate
        category.Reactivate();
        category.IsActive.Should().BeTrue();
    }
}
