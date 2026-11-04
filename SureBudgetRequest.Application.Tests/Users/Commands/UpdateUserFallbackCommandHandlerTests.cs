using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Users.Commands.UpdateUserFallback;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Users.Commands;

public class UpdateUserFallbackCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_WhenActingAsFallback_ShouldFailWithForbidden()
    {
        // Arrange
        _currentUser.IsFallbackPersonAct.Returns(true);
        var handler = new UpdateUserFallbackCommandHandler(_userRepository, _currentUser, _unitOfWork);
        var command = new UpdateUserFallbackCommand(Guid.NewGuid(), Guid.NewGuid(), false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.FallbackCannotModifyFallback.Code);
    }

    [Fact]
    public async Task Handle_WhenUpdatingAnotherUserAndNotAdmin_ShouldFailWithForbidden()
    {
        // Arrange
        var myId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        _currentUser.IsFallbackPersonAct.Returns(false);
        _currentUser.RealActorUserId.Returns(myId);
        _currentUser.Role.Returns(UserRole.Employee);

        var handler = new UpdateUserFallbackCommandHandler(_userRepository, _currentUser, _unitOfWork);
        var command = new UpdateUserFallbackCommand(otherUserId, Guid.NewGuid(), false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.Forbidden");
    }

    [Fact]
    public async Task Handle_WhenSelfAssigningAsFallback_ShouldFailWithValidationError()
    {
        // Arrange
        var myId = Guid.NewGuid();
        _currentUser.IsFallbackPersonAct.Returns(false);
        _currentUser.RealActorUserId.Returns(myId);

        var user = new User("myuser", "My User", "user@test.com", Guid.NewGuid(), UserRole.Employee);
        _userRepository.GetByIdAsync(myId, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new UpdateUserFallbackCommandHandler(_userRepository, _currentUser, _unitOfWork);
        var command = new UpdateUserFallbackCommand(myId, myId, false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.CannotAssignSelfAsFallback.Code);
    }

    [Fact]
    public async Task Handle_WhenTurningOnLeaveWithoutFallback_ShouldFailWithValidationError()
    {
        // Arrange
        var myId = Guid.NewGuid();
        _currentUser.IsFallbackPersonAct.Returns(false);
        _currentUser.RealActorUserId.Returns(myId);

        var user = new User("myuser", "My User", "user@test.com", Guid.NewGuid(), UserRole.Employee);
        _userRepository.GetByIdAsync(myId, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new UpdateUserFallbackCommandHandler(_userRepository, _currentUser, _unitOfWork);
        var command = new UpdateUserFallbackCommand(myId, null, true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.LeaveRequiresFallback.Code);
    }

    [Fact]
    public async Task Handle_ValidFallbackAndLeave_ShouldUpdateAndSave()
    {
        // Arrange
        var myId = Guid.NewGuid();
        var fallbackId = Guid.NewGuid();
        _currentUser.IsFallbackPersonAct.Returns(false);
        _currentUser.RealActorUserId.Returns(myId);

        var user = new User("myuser", "My User", "user@test.com", Guid.NewGuid(), UserRole.Employee);
        var fallbackUser = new User("fallback", "Fallback User", "fb@test.com", Guid.NewGuid(), UserRole.Employee);

        _userRepository.GetByIdAsync(myId, Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetByIdAsync(fallbackId, Arg.Any<CancellationToken>()).Returns(fallbackUser);

        var handler = new UpdateUserFallbackCommandHandler(_userRepository, _currentUser, _unitOfWork);
        var command = new UpdateUserFallbackCommand(myId, fallbackId, true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.FallbackUserId.Should().Be(fallbackId);
        user.IsOnLeave.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
