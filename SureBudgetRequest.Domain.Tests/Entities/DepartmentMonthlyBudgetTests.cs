using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class DepartmentMonthlyBudgetTests
{
    // =========================================================================
    // 1. Constructor & Temporal / Amount Invariants
    // =========================================================================

    [Fact]
    public void Constructor_ValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var createdById = Guid.NewGuid();
        var before = DateTime.UtcNow;

        // Act
        var budget = new DepartmentMonthlyBudget(
            deptId,
            year: 2026,
            month: 7,
            amount: 25_000_000m,
            createdById);
        var after = DateTime.UtcNow;

        // Assert
        budget.Id.Should().NotBeEmpty();
        budget.DepartmentId.Should().Be(deptId);
        budget.Year.Should().Be(2026);
        budget.Month.Should().Be(7);
        budget.Amount.Should().Be(25_000_000m);
        budget.CreatedByUserId.Should().Be(createdById);
        budget.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        budget.UpdatedByUserId.Should().BeNull();
        budget.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_ZeroAmount_ShouldBeAllowed()
    {
        // Act
        var budget = new DepartmentMonthlyBudget(Guid.NewGuid(), 2026, 1, 0m, Guid.NewGuid());

        // Assert
        budget.Amount.Should().Be(0m);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    [InlineData(-5_000_000)]
    public void Constructor_NegativeAmount_ShouldThrowArgumentException(decimal negativeAmount)
    {
        // Act
        Action act = () => new DepartmentMonthlyBudget(
            Guid.NewGuid(), 2026, 5, negativeAmount, Guid.NewGuid());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("amount")
            .WithMessage("*Amount cannot be negative.*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    public void Constructor_ValidMonths_ShouldSucceed(int validMonth)
    {
        // Act
        var budget = new DepartmentMonthlyBudget(
            Guid.NewGuid(), 2026, validMonth, 1_000_000m, Guid.NewGuid());

        // Assert
        budget.Month.Should().Be(validMonth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(99)]
    public void Constructor_InvalidMonth_ShouldThrowArgumentOutOfRangeException(int invalidMonth)
    {
        // Act
        Action act = () => new DepartmentMonthlyBudget(
            Guid.NewGuid(), 2026, invalidMonth, 1_000_000m, Guid.NewGuid());

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("month")
            .WithMessage("*Month must be between 1 and 12.*");
    }

    [Theory]
    [InlineData(2001)]
    [InlineData(2026)]
    [InlineData(2099)]
    public void Constructor_ValidYear_ShouldSucceed(int validYear)
    {
        // Act
        var budget = new DepartmentMonthlyBudget(
            Guid.NewGuid(), validYear, 1, 1_000_000m, Guid.NewGuid());

        // Assert
        budget.Year.Should().Be(validYear);
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(1999)]
    [InlineData(0)]
    [InlineData(-2026)]
    public void Constructor_InvalidYear_ShouldThrowArgumentOutOfRangeException(int invalidYear)
    {
        // Act
        Action act = () => new DepartmentMonthlyBudget(
            Guid.NewGuid(), invalidYear, 1, 1_000_000m, Guid.NewGuid());

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("year")
            .WithMessage("*Year must be greater than 2000.*");
    }

    // =========================================================================
    // 2. Update Method Tests
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(500_000)]
    [InlineData(100_000_000.50)]
    public void Update_ValidAmount_ShouldUpdateAmountAndAuditDetails(decimal newAmount)
    {
        // Arrange
        var budget = new DepartmentMonthlyBudget(
            Guid.NewGuid(), 2026, 6, 10_000_000m, Guid.NewGuid());
        var updatedById = Guid.NewGuid();
        var before = DateTime.UtcNow;

        // Act
        budget.Update(newAmount, updatedById);
        var after = DateTime.UtcNow;

        // Assert
        budget.Amount.Should().Be(newAmount);
        budget.UpdatedByUserId.Should().Be(updatedById);
        budget.UpdatedAt.Should().NotBeNull();
        budget.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    [InlineData(-10_000_000)]
    public void Update_NegativeAmount_ShouldThrowArgumentException(decimal negativeAmount)
    {
        // Arrange
        var budget = new DepartmentMonthlyBudget(
            Guid.NewGuid(), 2026, 6, 10_000_000m, Guid.NewGuid());

        // Act
        Action act = () => budget.Update(negativeAmount, Guid.NewGuid());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newAmount")
            .WithMessage("*Amount cannot be negative.*");
    }
}
