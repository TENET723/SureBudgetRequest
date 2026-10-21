using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Security;
using SureBudgetRequest.Application.Users.Commands.ChangePassword;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Users.Commands;

public class ChangePasswordCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ChangePasswordCommandHandler _handler;

    public ChangePasswordCommandHandlerTests()
    {
        _handler = new ChangePasswordCommandHandler(
            _userRepository,
            _passwordHasher,
            _unitOfWork);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("12345")]
    public async Task Handle_WhenNewPasswordIsShorterThanMinimum_ReturnsPasswordTooShortError(string? shortPassword)
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), "OldPass123!", shortPassword!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.PasswordTooShort(ChangePasswordCommandHandler.MinimumPasswordLength));

        await _userRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNewPasswordEqualsCurrentPassword_ReturnsPasswordsMustBeDifferentError()
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), "IdenticalPassword123!", "IdenticalPassword123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.PasswordsMustBeDifferent);

        await _userRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsGenericNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new ChangePasswordCommand(userId, "OldPass123!", "NewPass123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.GenericNotFound);

        _passwordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserIsInactive_ReturnsGenericNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("inactive", "Inactive User", "inactive@sure.com", Guid.NewGuid(), UserRole.Employee);
        user.SetPasswordHash("some_hash", mustChangeOnNextLogin: true);
        user.Deactivate();

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new ChangePasswordCommand(userId, "OldPass123!", "NewPass123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.GenericNotFound);

        _passwordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCurrentPasswordIncorrect_ReturnsCurrentPasswordIncorrectError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("user1", "User One", "user1@sure.com", Guid.NewGuid(), UserRole.Employee);
        user.SetPasswordHash("existing_hash", mustChangeOnNextLogin: true);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("existing_hash", "WrongOldPass123!").Returns(false);

        var command = new ChangePasswordCommand(userId, "WrongOldPass123!", "NewPass123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.CurrentPasswordIncorrect);

        _passwordHasher.DidNotReceive().Hash(Arg.Any<string>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValid_HashesNewPasswordClearsMustChangeFlagAndSaves()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("user1", "User One", "user1@sure.com", Guid.NewGuid(), UserRole.Employee);
        user.SetPasswordHash("old_hash", mustChangeOnNextLogin: true);
        user.MustChangePassword.Should().BeTrue();

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("old_hash", "CorrectOldPass123!").Returns(true);
        _passwordHasher.Hash("BrandNewPass456!").Returns("newly_computed_hash");

        var command = new ChangePasswordCommand(userId, "CorrectOldPass123!", "BrandNewPass456!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be("newly_computed_hash");
        user.MustChangePassword.Should().BeFalse();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("user1", "User One", "user1@sure.com", Guid.NewGuid(), UserRole.Employee);
        user.SetPasswordHash("old_hash", mustChangeOnNextLogin: false);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _userRepository.GetByIdAsync(userId, ct).Returns(user);
        _passwordHasher.Verify("old_hash", "OldPass123!").Returns(true);
        _passwordHasher.Hash("NewPass123!").Returns("new_hash");

        var command = new ChangePasswordCommand(userId, "OldPass123!", "NewPass123!");

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userRepository.Received(1).GetByIdAsync(userId, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
