using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Security;
using SureBudgetRequest.Application.Users.Commands.ResetUserPassword;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Users.Commands;

public class ResetUserPasswordCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ResetUserPasswordCommandHandler _handler;

    public ResetUserPasswordCommandHandlerTests()
    {
        _handler = new ResetUserPasswordCommandHandler(
            _userRepository,
            _passwordHasher,
            _unitOfWork);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("12345")]
    public async Task Handle_WhenPasswordIsShorterThanMinimum_ReturnsPasswordTooShortError(string? shortPassword)
    {
        // Arrange
        var command = new ResetUserPasswordCommand(Guid.NewGuid(), shortPassword!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.PasswordTooShort(ResetUserPasswordCommandHandler.MinimumPasswordLength));

        await _userRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsGenericNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new ResetUserPasswordCommand(userId, "NewResetPass123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.GenericNotFound);

        _passwordHasher.DidNotReceive().Hash(Arg.Any<string>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValid_HashesPasswordSetsMustChangeFlagTrueAndSaves()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("alice", "Alice Smith", "alice@sure.com", Guid.NewGuid(), UserRole.Employee);
        user.SetPasswordHash("previous_hash", mustChangeOnNextLogin: false);
        user.MustChangePassword.Should().BeFalse();

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Hash("ResetPassword456!").Returns("new_reset_hash");

        var command = new ResetUserPasswordCommand(userId, "ResetPassword456!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be("new_reset_hash");
        user.MustChangePassword.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("alice", "Alice Smith", "alice@sure.com", Guid.NewGuid(), UserRole.Employee);
        user.SetPasswordHash("previous_hash", mustChangeOnNextLogin: false);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _userRepository.GetByIdAsync(userId, ct).Returns(user);
        _passwordHasher.Hash("ResetPassword456!").Returns("new_reset_hash");

        var command = new ResetUserPasswordCommand(userId, "ResetPassword456!");

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userRepository.Received(1).GetByIdAsync(userId, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
