using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Security;
using SureBudgetRequest.Application.Users.Commands.AuthenticateUser;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Users.Commands;

public class AuthenticateUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AuthenticateUserCommandHandler _handler;

    public AuthenticateUserCommandHandlerTests()
    {
        _handler = new AuthenticateUserCommandHandler(
            _userRepository,
            _passwordHasher,
            _unitOfWork);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenEmailIsNullOrWhiteSpace_ReturnsInvalidCredentialsError(string? invalidEmail)
    {
        // Arrange
        var command = new AuthenticateUserCommand(invalidEmail!, "ValidPass123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidCredentials);

        await _userRepository.DidNotReceive().GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenPasswordIsNullOrWhiteSpace_ReturnsInvalidCredentialsError(string? invalidPassword)
    {
        // Arrange
        var command = new AuthenticateUserCommand("user@sure.com", invalidPassword!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidCredentials);

        await _userRepository.DidNotReceive().GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsInvalidCredentialsError()
    {
        // Arrange
        _userRepository.GetByEmailAsync("notfound@sure.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var command = new AuthenticateUserCommand("notfound@sure.com", "SecretPass123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidCredentials);

        _passwordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserIsInactive_ReturnsInvalidCredentialsError()
    {
        // Arrange
        var user = new User("inactiveuser", "Inactive User", "inactive@sure.com", Guid.NewGuid(), UserRole.Employee);
        user.SetPasswordHash("some_hash", mustChangeOnNextLogin: false);
        user.Deactivate();

        _userRepository.GetByEmailAsync("inactive@sure.com", Arg.Any<CancellationToken>())
            .Returns(user);

        var command = new AuthenticateUserCommand("inactive@sure.com", "SecretPass123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidCredentials);

        _passwordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPasswordVerificationFails_ReturnsInvalidCredentialsError()
    {
        // Arrange
        var user = new User("john", "John Doe", "john@sure.com", Guid.NewGuid(), UserRole.Employee);
        user.SetPasswordHash("hashed_val", mustChangeOnNextLogin: false);

        _userRepository.GetByEmailAsync("john@sure.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.Verify("hashed_val", "WrongPassword!").Returns(false);

        var command = new AuthenticateUserCommand("john@sure.com", "WrongPassword!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidCredentials);

        user.LastLoginAt.Should().BeNull();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCredentialsAreValid_RecordsLoginSavesAndReturnsDto()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new User("alice", "Alice Smith", "alice@sure.com", deptId, UserRole.Finance);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, userId);
        user.SetPasswordHash("hashed_alice", mustChangeOnNextLogin: true);
        user.SetFinanceApprover(true);

        _userRepository.GetByEmailAsync("alice@sure.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.Verify("hashed_alice", "CorrectPassword123!").Returns(true);

        var command = new AuthenticateUserCommand("alice@sure.com", "CorrectPassword123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(userId);
        result.Value.Username.Should().Be("alice");
        result.Value.Email.Should().Be("alice@sure.com");
        result.Value.FullName.Should().Be("Alice Smith");
        result.Value.Role.Should().Be(UserRole.Finance);
        result.Value.DepartmentId.Should().Be(deptId);
        result.Value.MustChangePassword.Should().BeTrue();
        result.Value.IsFinanceApprover.Should().BeTrue();

        user.LastLoginAt.Should().NotBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var user = new User("alice", "Alice Smith", "alice@sure.com", Guid.NewGuid(), UserRole.Finance);
        user.SetPasswordHash("hashed_alice", mustChangeOnNextLogin: false);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _userRepository.GetByEmailAsync("alice@sure.com", ct).Returns(user);
        _passwordHasher.Verify("hashed_alice", "CorrectPassword123!").Returns(true);

        var command = new AuthenticateUserCommand("alice@sure.com", "CorrectPassword123!");

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userRepository.Received(1).GetByEmailAsync("alice@sure.com", ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
