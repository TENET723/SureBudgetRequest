using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class DepartmentTests
{
    private const string ValidWebhookUrl = "https://hooks.slack.com/services/test-channel/mock-token";

    // =========================================================================
    // 1. Constructor & Validation
    // =========================================================================

    [Fact]
    public void Constructor_ValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        var headId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        // Act
        var dept = new Department(
            "Engineering",
            headId,
            budgetLimit: 500_000m,
            monthlyLimit: 5_000_000m,
            slackWebhookUrl: ValidWebhookUrl);
        var after = DateTime.UtcNow;

        // Assert
        dept.Name.Should().Be("Engineering");
        dept.HeadUserId.Should().Be(headId);
        dept.BudgetLimit.Should().Be(500_000m);
        dept.MonthlyLimit.Should().Be(5_000_000m);
        dept.SlackWebhookUrl.Should().Be(ValidWebhookUrl);
        dept.IsActive.Should().BeTrue();
        dept.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Constructor_WithNullHeadAndWebhook_ShouldAllowNulls()
    {
        // Act
        var dept = new Department("Marketing", null, 0m, 0m, null);

        // Assert
        dept.HeadUserId.Should().BeNull();
        dept.SlackWebhookUrl.Should().BeNull();
        dept.BudgetLimit.Should().Be(0m);
        dept.MonthlyLimit.Should().Be(0m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        // Act
        Action act = () => new Department(invalidName!, null, 100_000m, 1_000_000m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Department name is required.*");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    [InlineData(-1_000_000)]
    public void Constructor_NegativeBudgetLimit_ShouldThrowArgumentException(decimal negativeLimit)
    {
        // Act
        Action act = () => new Department("HR", null, negativeLimit, 1_000_000m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("budgetLimit")
            .WithMessage("*Budget limit cannot be negative.*");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    [InlineData(-5_000_000)]
    public void Constructor_NegativeMonthlyLimit_ShouldThrowArgumentException(decimal negativeMonthlyLimit)
    {
        // Act
        Action act = () => new Department("Finance", null, 500_000m, negativeMonthlyLimit);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("monthlyLimit")
            .WithMessage("*Monthly limit cannot be negative.*");
    }

    // =========================================================================
    // 2. Slack Webhook Normalization & Validation
    // =========================================================================

    [Theory]
    [InlineData("https://hooks.slack.com/services/abc/def/123")]
    [InlineData("  https://hooks.slack.com/services/abc/def/123  ")]
    [InlineData("HTTPS://HOOKS.SLACK.COM/SERVICES/ABC/DEF/123")]
    public void Webhook_ValidSlackUrl_ShouldTrimAndStore(string inputUrl)
    {
        // Act
        var dept = new Department("Sales", null, 0m, 0m, inputUrl);

        // Assert
        dept.SlackWebhookUrl.Should().Be(inputUrl.Trim());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Webhook_NullOrWhitespace_ShouldStoreNull(string? emptyUrl)
    {
        // Act
        var dept = new Department("Sales", null, 0m, 0m, emptyUrl);

        // Assert
        dept.SlackWebhookUrl.Should().BeNull();
    }

    [Theory]
    [InlineData("http://hooks.slack.com/services/abc/def/123")] // insecure HTTP
    [InlineData("https://other-domain.com/webhook")]
    [InlineData("https://hooks.slack.com/not-services/")]
    [InlineData("ftp://hooks.slack.com/services/123")]
    [InlineData("invalid-url-string")]
    public void Webhook_InvalidPrefix_ShouldThrowArgumentException(string invalidUrl)
    {
        // Act
        Action act = () => new Department("Operations", null, 0m, 0m, invalidUrl);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("url")
            .WithMessage("*Slack webhook URL must start with 'https://hooks.slack.com/services/'.*");
    }

    [Fact]
    public void SetSlackWebhookUrl_ShouldUpdateOrClearWebhook()
    {
        // Arrange
        var dept = new Department("Operations", null, 0m, 0m, ValidWebhookUrl);
        dept.SlackWebhookUrl.Should().Be(ValidWebhookUrl);

        // Act 1: Clear webhook
        dept.SetSlackWebhookUrl(null);
        dept.SlackWebhookUrl.Should().BeNull();

        // Act 2: Set new webhook
        const string newUrl = "https://hooks.slack.com/services/NEW/NEW/NEW";
        dept.SetSlackWebhookUrl(newUrl);
        dept.SlackWebhookUrl.Should().Be(newUrl);

        // Act 3: Clear with whitespace
        dept.SetSlackWebhookUrl("   ");
        dept.SlackWebhookUrl.Should().BeNull();
    }

    // =========================================================================
    // 3. Mutation Methods (Rename, ChangeHead, ChangeLimits)
    // =========================================================================

    [Theory]
    [InlineData("Engineering Core")]
    [InlineData("  DevOps  ")]
    public void Rename_ValidName_ShouldUpdateName(string newName)
    {
        // Arrange
        var dept = new Department("Old Name", null, 0m, 0m);

        // Act
        dept.Rename(newName);

        // Assert
        dept.Name.Should().Be(newName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_InvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        // Arrange
        var dept = new Department("Valid", null, 0m, 0m);

        // Act
        Action act = () => dept.Rename(invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newName")
            .WithMessage("*Department name is required.*");
    }

    [Fact]
    public void ChangeHead_ShouldUpdateHeadUserId()
    {
        // Arrange
        var dept = new Department("IT", null, 0m, 0m);
        dept.HeadUserId.Should().BeNull();

        var newHead = Guid.NewGuid();

        // Act 1: Assign head
        dept.ChangeHead(newHead);
        dept.HeadUserId.Should().Be(newHead);

        // Act 2: Unassign head
        dept.ChangeHead(null);
        dept.HeadUserId.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100_000)]
    [InlineData(2_500_000.75)]
    public void ChangeBudgetLimit_ValidLimit_ShouldUpdateLimit(decimal newLimit)
    {
        // Arrange
        var dept = new Department("Design", null, 50_000m, 100_000m);

        // Act
        dept.ChangeBudgetLimit(newLimit);

        // Assert
        dept.BudgetLimit.Should().Be(newLimit);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void ChangeBudgetLimit_NegativeLimit_ShouldThrowArgumentException(decimal negativeLimit)
    {
        // Arrange
        var dept = new Department("Design", null, 50_000m, 100_000m);

        // Act
        Action act = () => dept.ChangeBudgetLimit(negativeLimit);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newLimit")
            .WithMessage("*Budget limit cannot be negative.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500_000)]
    [InlineData(10_000_000.50)]
    public void ChangeMonthlyLimit_ValidLimit_ShouldUpdateLimit(decimal newLimit)
    {
        // Arrange
        var dept = new Department("Security", null, 50_000m, 100_000m);

        // Act
        dept.ChangeMonthlyLimit(newLimit);

        // Assert
        dept.MonthlyLimit.Should().Be(newLimit);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void ChangeMonthlyLimit_NegativeLimit_ShouldThrowArgumentException(decimal negativeLimit)
    {
        // Arrange
        var dept = new Department("Security", null, 50_000m, 100_000m);

        // Act
        Action act = () => dept.ChangeMonthlyLimit(negativeLimit);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newLimit")
            .WithMessage("*Monthly limit cannot be negative.*");
    }

    // =========================================================================
    // 4. Deactivate & Reactivate
    // =========================================================================

    [Fact]
    public void DeactivateAndReactivate_ShouldToggleIsActive()
    {
        // Arrange
        var dept = new Department("QA", null, 0m, 0m);
        dept.IsActive.Should().BeTrue();

        // Act 1: Deactivate
        dept.Deactivate();
        dept.IsActive.Should().BeFalse();

        // Act 2: Reactivate
        dept.Reactivate();
        dept.IsActive.Should().BeTrue();
    }
}
