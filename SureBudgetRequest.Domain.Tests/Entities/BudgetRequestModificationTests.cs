using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class BudgetRequestModificationTests
{
    [Fact]
    public void Constructor_ValidParameters_ShouldInitializeProperties()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var note = "  Amount changed from 100k to 120k  ";
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var mod = new BudgetRequestModification(reqId, userId, note);
        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        mod.BudgetRequestId.Should().Be(reqId);
        mod.ModifiedByUserId.Should().Be(userId);
        mod.Note.Should().Be("Amount changed from 100k to 120k");
        mod.ModifiedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceNote_ShouldStoreAsNull(string? note)
    {
        // Act
        var mod = new BudgetRequestModification(Guid.NewGuid(), Guid.NewGuid(), note);

        // Assert
        mod.Note.Should().BeNull();
    }

    [Fact]
    public void Constructor_EmptyBudgetRequestId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => new BudgetRequestModification(Guid.Empty, Guid.NewGuid(), "Note");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("budgetRequestId")
            .WithMessage("*Budget request ID is required.*");
    }

    [Fact]
    public void Constructor_EmptyModifiedByUserId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => new BudgetRequestModification(Guid.NewGuid(), Guid.Empty, "Note");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("modifiedByUserId")
            .WithMessage("*Modified by user ID is required.*");
    }
}
