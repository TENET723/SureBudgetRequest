using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class CoaTests
{
    // =========================================================================
    // 1. Constructor & Defaults
    // =========================================================================

    [Fact]
    public void Constructor_ValidParameters_ShouldTrimAndInitialize()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var coa = new Coa("  5110  ", "  Office Supplies  ", "  General desk items  ");
        var after = DateTime.UtcNow;

        // Assert
        coa.Code.Should().Be("5110");
        coa.Name.Should().Be("Office Supplies");
        coa.Description.Should().Be("General desk items");
        coa.IsActive.Should().BeTrue();
        coa.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyOrWhitespaceDescription_ShouldStoreAsNull(string? description)
    {
        // Act
        var coa = new Coa("5120", "Travel Expenses", description);

        // Assert
        coa.Description.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidCode_ShouldThrowArgumentException(string? invalidCode)
    {
        // Act
        Action act = () => new Coa(invalidCode!, "Valid Name");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("code")
            .WithMessage("*Code is required.*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        // Act
        Action act = () => new Coa("5130", invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Name is required.*");
    }

    // =========================================================================
    // 2. Mutation Methods
    // =========================================================================

    [Theory]
    [InlineData("IT Hardware")]
    [InlineData("  IT Infrastructure Hardware  ")]
    public void Rename_ValidName_ShouldUpdateAndTrimName(string newName)
    {
        // Arrange
        var coa = new Coa("6000", "Old Name");

        // Act
        coa.Rename(newName);

        // Assert
        coa.Name.Should().Be(newName.Trim());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_InvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        // Arrange
        var coa = new Coa("6000", "Valid Name");

        // Act
        Action act = () => coa.Rename(invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newName")
            .WithMessage("*Name is required.*");
    }

    [Theory]
    [InlineData("5115")]
    [InlineData("  5125  ")]
    public void ChangeCode_ValidCode_ShouldUpdateAndTrimCode(string newCode)
    {
        // Arrange
        var coa = new Coa("5110", "Supplies");

        // Act
        coa.ChangeCode(newCode);

        // Assert
        coa.Code.Should().Be(newCode.Trim());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangeCode_InvalidCode_ShouldThrowArgumentException(string? invalidCode)
    {
        // Arrange
        var coa = new Coa("5110", "Supplies");

        // Act
        Action act = () => coa.ChangeCode(invalidCode!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newCode")
            .WithMessage("*Code is required.*");
    }

    [Fact]
    public void ChangeDescription_ShouldUpdateOrClearDescription()
    {
        // Arrange
        var coa = new Coa("5110", "Supplies", "Initial description");

        // Act 1: Update description
        coa.ChangeDescription("  Updated description  ");
        coa.Description.Should().Be("Updated description");

        // Act 2: Clear with null
        coa.ChangeDescription(null);
        coa.Description.Should().BeNull();

        // Act 3: Clear with whitespace
        coa.ChangeDescription("   ");
        coa.Description.Should().BeNull();
    }

    // =========================================================================
    // 3. Deactivate & Reactivate
    // =========================================================================

    [Fact]
    public void DeactivateAndReactivate_ShouldToggleIsActive()
    {
        // Arrange
        var coa = new Coa("7000", "Miscellaneous");
        coa.IsActive.Should().BeTrue();

        // Act 1: Deactivate
        coa.Deactivate();
        coa.IsActive.Should().BeFalse();

        // Act 2: Reactivate
        coa.Reactivate();
        coa.IsActive.Should().BeTrue();
    }
}
