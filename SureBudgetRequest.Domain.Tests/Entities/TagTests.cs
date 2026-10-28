using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class TagTests
{
    private readonly Guid _deptId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        var before = DateTime.UtcNow;
        var tag = new Tag(_deptId, "  Campaign-Q4  ", _userId, "#3b82f6");
        var after = DateTime.UtcNow;

        tag.DepartmentId.Should().Be(_deptId);
        tag.Name.Should().Be("Campaign-Q4");
        tag.ColorHex.Should().Be("#3B82F6");
        tag.CreatedByUserId.Should().Be(_userId);
        tag.IsActive.Should().BeTrue();
        tag.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Constructor_WithoutColorHex_ShouldHaveNullColor()
    {
        var tag = new Tag(_deptId, "Offsite", _userId);

        tag.ColorHex.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenDepartmentIdIsEmpty()
    {
        var act = () => new Tag(Guid.Empty, "Vendor", _userId);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("departmentId")
            .WithMessage("*Department ID is required*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenNameIsNullOrWhiteSpace(string? name)
    {
        var act = () => new Tag(_deptId, name!, _userId);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Tag name is required*");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenCreatedByUserIdIsEmpty()
    {
        var act = () => new Tag(_deptId, "Vendor", Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("createdByUserId")
            .WithMessage("*Created by user ID is required*");
    }

    [Theory]
    [InlineData("#FFF", "#FFF")]
    [InlineData("#abc", "#ABC")]
    [InlineData("#10B981", "#10B981")]
    [InlineData("#ef4444", "#EF4444")]
    [InlineData("  #3B82F6  ", "#3B82F6")]
    public void Constructor_ShouldNormalizeValidHexColor(string inputColor, string expectedColor)
    {
        var tag = new Tag(_deptId, "Urgent", _userId, inputColor);

        tag.ColorHex.Should().Be(expectedColor);
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("123456")]
    [InlineData("#GGGGGG")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    public void Constructor_ShouldThrowArgumentException_WhenColorHexIsInvalid(string invalidHex)
    {
        var act = () => new Tag(_deptId, "Urgent", _userId, invalidHex);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("colorHex")
            .WithMessage("*valid hex color code*");
    }

    [Fact]
    public void Rename_WithValidName_ShouldUpdateAndTrim()
    {
        var tag = new Tag(_deptId, "Original", _userId);

        tag.Rename("  UpdatedName  ");

        tag.Name.Should().Be("UpdatedName");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_WithNullOrWhiteSpace_ShouldThrowArgumentException(string? invalidName)
    {
        var tag = new Tag(_deptId, "Original", _userId);

        var act = () => tag.Rename(invalidName!);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("newName")
            .WithMessage("*Tag name is required*");
    }

    [Fact]
    public void SetColor_WithValidHex_ShouldUpdateColor()
    {
        var tag = new Tag(_deptId, "Tag", _userId, "#111");

        tag.SetColor("#334455");

        tag.ColorHex.Should().Be("#334455");
    }

    [Fact]
    public void SetColor_WithNullOrWhiteSpace_ShouldClearColor()
    {
        var tag = new Tag(_deptId, "Tag", _userId, "#3B82F6");

        tag.SetColor(null);
        tag.ColorHex.Should().BeNull();

        tag.SetColor("#FFF");
        tag.SetColor("   ");
        tag.ColorHex.Should().BeNull();
    }

    [Fact]
    public void SetColor_WithInvalidHex_ShouldThrowArgumentException()
    {
        var tag = new Tag(_deptId, "Tag", _userId);

        var act = () => tag.SetColor("not-a-color");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("colorHex");
    }

    [Fact]
    public void DeactivateAndReactivate_ShouldToggleIsActive()
    {
        var tag = new Tag(_deptId, "Tag", _userId);
        tag.IsActive.Should().BeTrue();

        tag.Deactivate();
        tag.IsActive.Should().BeFalse();

        tag.Reactivate();
        tag.IsActive.Should().BeTrue();
    }
}
