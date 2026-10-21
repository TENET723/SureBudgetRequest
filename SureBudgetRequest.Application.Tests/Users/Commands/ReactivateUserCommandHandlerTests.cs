using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Users.Commands.ReactivateUser;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Users.Commands;

public class ReactivateUserCommandHandlerTests
{
    private readonly IUserRepository _repository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ReactivateUserCommandHandler _handler;

    public ReactivateUserCommandHandlerTests()
    {
        _handler = new ReactivateUserCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsGenericNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new ReactivateUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.GenericNotFound);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserExistsAndInactive_ReactivatesAndSaves()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("inactiveuser", "Inactive User", "inactive@sure.com", Guid.NewGuid(), UserRole.Employee);
        user.Deactivate();
        user.IsActive.Should().BeFalse();

        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new ReactivateUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyActive_IdempotentlyReactivatesAndSaves()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("activeuser", "Active User", "active@sure.com", Guid.NewGuid(), UserRole.Employee);
        user.IsActive.Should().BeTrue();

        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new ReactivateUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("user1", "User One", "user1@sure.com", Guid.NewGuid(), UserRole.Employee);
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _repository.GetByIdAsync(userId, ct).Returns(user);

        var command = new ReactivateUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByIdAsync(userId, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
