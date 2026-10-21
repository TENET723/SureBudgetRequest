using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class WithdrawMethodTests
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
        var method = new WithdrawMethod("  Bank Transfer  ", requiresAttachment: true);
        var after = DateTime.UtcNow;

        // Assert
        method.Name.Should().Be("Bank Transfer");
        method.RequiresAttachment.Should().BeTrue();
        method.IsActive.Should().BeTrue();
        method.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Constructor_DefaultRequiresAttachment_ShouldBeFalse()
    {
        // Act
        var method = new WithdrawMethod("Cash");

        // Assert
        method.Name.Should().Be("Cash");
        method.RequiresAttachment.Should().BeFalse();
        method.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        // Act
        Action act = () => new WithdrawMethod(invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Name is required.*");
    }

    // =========================================================================
    // 2. Rename & Mutation Tests
    // =========================================================================

    [Theory]
    [InlineData("Cheque")]
    [InlineData("  Electronic Cheque  ")]
    public void Rename_ValidName_ShouldUpdateAndTrimName(string newName)
    {
        // Arrange
        var method = new WithdrawMethod("Old Name");

        // Act
        method.Rename(newName);

        // Assert
        method.Name.Should().Be(newName.Trim());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_InvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        // Arrange
        var method = new WithdrawMethod("Valid Name");

        // Act
        Action act = () => method.Rename(invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newName")
            .WithMessage("*Name is required.*");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetRequiresAttachment_ShouldUpdateFlag(bool flagValue)
    {
        // Arrange
        var method = new WithdrawMethod("Cash", requiresAttachment: !flagValue);

        // Act
        method.SetRequiresAttachment(flagValue);

        // Assert
        method.RequiresAttachment.Should().Be(flagValue);
    }

    // =========================================================================
    // 3. Deactivate & Reactivate
    // =========================================================================

    [Fact]
    public void DeactivateAndReactivate_ShouldToggleIsActive()
    {
        // Arrange
        var method = new WithdrawMethod("Cheque");
        method.IsActive.Should().BeTrue();

        // Act 1: Deactivate
        method.Deactivate();
        method.IsActive.Should().BeFalse();

        // Act 2: Reactivate
        method.Reactivate();
        method.IsActive.Should().BeTrue();
    }
}
