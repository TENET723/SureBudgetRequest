using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class UserTests
{
    // =========================================================================
    // 1. Constructor & Invariant Defaults
    // =========================================================================

    [Fact]
    public void Constructor_ValidParameters_ShouldInitializeCorrectDefaults()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        // Act
        var user = new User(
            "john.doe",
            "John Doe",
            "  John.Doe@Company.COM  ",
            deptId,
            UserRole.Employee);
        var after = DateTime.UtcNow;

        // Assert
        user.Username.Should().Be("john.doe");
        user.FullName.Should().Be("John Doe");
        user.Email.Should().Be("john.doe@company.com"); // Normalized
        user.DepartmentId.Should().Be(deptId);
        user.Role.Should().Be(UserRole.Employee);
        user.IsActive.Should().BeTrue();
        user.MustChangePassword.Should().BeTrue(); // Default security constraint
        user.IsFinanceApprover.Should().BeFalse(); // Default
        user.LastLoginAt.Should().BeNull();
        user.SlackUserId.Should().BeNull();
        user.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Theory]
    [InlineData("user@domain.com", "user@domain.com")]
    [InlineData("USER@DOMAIN.COM", "user@domain.com")]
    [InlineData("  First.Last@Domain.Co.UK  ", "first.last@domain.co.uk")]
    public void Constructor_Email_ShouldBeTrimmedAndLowerCased(string rawEmail, string expectedEmail)
    {
        // Act
        var user = new User("user1", "User One", rawEmail, Guid.NewGuid(), UserRole.Employee);

        // Assert
        user.Email.Should().Be(expectedEmail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidUsername_ShouldThrowArgumentException(string? invalidUsername)
    {
        // Act
        Action act = () => new User(invalidUsername!, "Name", "user@co.com", Guid.NewGuid(), UserRole.Employee);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("username")
            .WithMessage("*Username is required.*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidFullName_ShouldThrowArgumentException(string? invalidName)
    {
        // Act
        Action act = () => new User("user1", invalidName!, "user@co.com", Guid.NewGuid(), UserRole.Employee);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("fullName")
            .WithMessage("*Full name is required.*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidEmail_ShouldThrowArgumentException(string? invalidEmail)
    {
        // Act
        Action act = () => new User("user1", "User One", invalidEmail!, Guid.NewGuid(), UserRole.Employee);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("email")
            .WithMessage("*Email is required.*");
    }

    // =========================================================================
    // 2. Role & Finance Approver Sub-Type Invariants
    // =========================================================================

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.DepartmentHead)]
    [InlineData(UserRole.Management)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Accounting)]
    [InlineData(UserRole.Support)]
    public void SetFinanceApprover_TrueOnNonFinanceUser_ShouldThrowInvalidOperationException(UserRole nonFinanceRole)
    {
        // Arrange
        var user = new User("test.user", "Test User", "test@co.com", Guid.NewGuid(), nonFinanceRole);

        // Act
        Action act = () => user.SetFinanceApprover(true);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Only a user with the Finance role can be marked as a Finance Approver.");
        user.IsFinanceApprover.Should().BeFalse();
    }

    [Fact]
    public void SetFinanceApprover_TrueOnFinanceUser_ShouldSucceed()
    {
        // Arrange
        var user = new User("fin.user", "Finance User", "fin@co.com", Guid.NewGuid(), UserRole.Finance);
        user.IsFinanceApprover.Should().BeFalse();

        // Act
        user.SetFinanceApprover(true);

        // Assert
        user.IsFinanceApprover.Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.DepartmentHead)]
    [InlineData(UserRole.Finance)]
    [InlineData(UserRole.Management)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Accounting)]
    [InlineData(UserRole.Support)]
    public void SetFinanceApprover_False_ShouldAlwaysSucceed(UserRole anyRole)
    {
        // Arrange
        var user = new User("test.user", "Test User", "test@co.com", Guid.NewGuid(), anyRole);

        // Act
        user.SetFinanceApprover(false);

        // Assert
        user.IsFinanceApprover.Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.DepartmentHead)]
    [InlineData(UserRole.Management)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Accounting)]
    [InlineData(UserRole.Support)]
    public void ChangeRole_FromFinanceWithApproverToNonFinance_ShouldAutomaticallyClearApproverFlag(UserRole newRole)
    {
        // Arrange: Finance approver
        var user = new User("fin.approver", "Finance Approver", "fa@co.com", Guid.NewGuid(), UserRole.Finance);
        user.SetFinanceApprover(true);
        user.IsFinanceApprover.Should().BeTrue();

        // Act: Role moved away from Finance
        user.ChangeRole(newRole);

        // Assert: Stale approver bit must be purged
        user.Role.Should().Be(newRole);
        user.IsFinanceApprover.Should().BeFalse();
    }

    [Fact]
    public void ChangeRole_ToFinance_ShouldPreserveRole()
    {
        // Arrange
        var user = new User("emp", "Employee", "emp@co.com", Guid.NewGuid(), UserRole.Employee);

        // Act
        user.ChangeRole(UserRole.Finance);

        // Assert
        user.Role.Should().Be(UserRole.Finance);
        user.IsFinanceApprover.Should().BeFalse();
    }

    // =========================================================================
    // 3. Password & Login Lifecycle
    // =========================================================================

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetPasswordHash_ValidHash_ShouldUpdateHashAndChangeFlag(bool mustChangePassword)
    {
        // Arrange
        var user = new User("user1", "User One", "user@co.com", Guid.NewGuid(), UserRole.Employee);
        const string mockHash = "v1$210000$salt$hash";

        // Act
        user.SetPasswordHash(mockHash, mustChangePassword);

        // Assert
        user.PasswordHash.Should().Be(mockHash);
        user.MustChangePassword.Should().Be(mustChangePassword);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetPasswordHash_InvalidHash_ShouldThrowArgumentException(string? invalidHash)
    {
        // Arrange
        var user = new User("user1", "User One", "user@co.com", Guid.NewGuid(), UserRole.Employee);

        // Act
        Action act = () => user.SetPasswordHash(invalidHash!, true);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("passwordHash")
            .WithMessage("*Password hash is required.*");
    }

    [Fact]
    public void RecordLogin_ShouldUpdateLastLoginAtToUtc()
    {
        // Arrange
        var user = new User("user1", "User One", "user@co.com", Guid.NewGuid(), UserRole.Employee);
        user.LastLoginAt.Should().BeNull();
        var before = DateTime.UtcNow;

        // Act
        user.RecordLogin();
        var after = DateTime.UtcNow;

        // Assert
        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    // =========================================================================
    // 4. Profile & Contact Mutations
    // =========================================================================

    [Theory]
    [InlineData("new.email@company.com", "new.email@company.com")]
    [InlineData("  New.Email@COMPANY.COM  ", "new.email@company.com")]
    public void SetEmail_ValidEmail_ShouldNormalizeAndSave(string rawEmail, string expectedEmail)
    {
        // Arrange
        var user = new User("user1", "User One", "old@co.com", Guid.NewGuid(), UserRole.Employee);

        // Act
        user.SetEmail(rawEmail);

        // Assert
        user.Email.Should().Be(expectedEmail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetEmail_InvalidEmail_ShouldThrowArgumentException(string? invalidEmail)
    {
        // Arrange
        var user = new User("user1", "User One", "valid@co.com", Guid.NewGuid(), UserRole.Employee);

        // Act
        Action act = () => user.SetEmail(invalidEmail!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("email")
            .WithMessage("*Email is required.*");
    }

    [Fact]
    public void Rename_ValidFullName_ShouldUpdateFullName()
    {
        // Arrange
        var user = new User("user1", "Old Name", "user@co.com", Guid.NewGuid(), UserRole.Employee);

        // Act
        user.Rename("New Full Name");

        // Assert
        user.FullName.Should().Be("New Full Name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_InvalidFullName_ShouldThrowArgumentException(string? invalidName)
    {
        // Arrange
        var user = new User("user1", "Valid Name", "user@co.com", Guid.NewGuid(), UserRole.Employee);

        // Act
        Action act = () => user.Rename(invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("fullName")
            .WithMessage("*Full name is required.*");
    }

    [Fact]
    public void ChangeDepartment_ShouldUpdateDepartmentId()
    {
        // Arrange
        var initialDept = Guid.NewGuid();
        var newDept = Guid.NewGuid();
        var user = new User("user1", "User One", "user@co.com", initialDept, UserRole.Employee);

        // Act
        user.ChangeDepartment(newDept);

        // Assert
        user.DepartmentId.Should().Be(newDept);
    }

    [Fact]
    public void SetSlackUserId_ShouldUpdateOrClearSlackId()
    {
        // Arrange
        var user = new User("user1", "User One", "user@co.com", Guid.NewGuid(), UserRole.Employee);

        // Act 1: Set Slack ID
        user.SetSlackUserId("U12345678");
        user.SlackUserId.Should().Be("U12345678");

        // Act 2: Clear Slack ID
        user.SetSlackUserId(null);
        user.SlackUserId.Should().BeNull();
    }

    // =========================================================================
    // 5. Deactivate & Reactivate
    // =========================================================================

    [Fact]
    public void DeactivateAndReactivate_ShouldToggleIsActive()
    {
        // Arrange
        var user = new User("user1", "User One", "user@co.com", Guid.NewGuid(), UserRole.Employee);
        user.IsActive.Should().BeTrue();

        // Act 1: Deactivate
        user.Deactivate();
        user.IsActive.Should().BeFalse();

        // Act 2: Reactivate
        user.Reactivate();
        user.IsActive.Should().BeTrue();
    }
}
