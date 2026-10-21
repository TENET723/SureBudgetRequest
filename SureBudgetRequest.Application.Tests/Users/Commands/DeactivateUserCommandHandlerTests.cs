using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Users.Commands.DeactivateUser;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Users.Commands;

public class DeactivateUserCommandHandlerTests
{
    private readonly IUserRepository _repository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DeactivateUserCommandHandler _handler;

    public DeactivateUserCommandHandlerTests()
    {
        _handler = new DeactivateUserCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsGenericNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new DeactivateUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.GenericNotFound);

        await _repository.DidNotReceive().CountActiveFinanceApproversAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public async Task Handle_WhenUserIsLastActiveFinanceApprover_ReturnsLastActiveFinanceApproverError(int approverCount)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("fin1", "Finance Approver", "fin1@sure.com", Guid.NewGuid(), UserRole.Finance);
        user.SetFinanceApprover(true);

        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _repository.CountActiveFinanceApproversAsync(Arg.Any<CancellationToken>()).Returns(approverCount);

        var command = new DeactivateUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.LastActiveFinanceApprover);
        user.IsActive.Should().BeTrue();

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserIsFinanceApproverAndOthersExist_DeactivatesAndSaves()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("fin1", "Finance Approver", "fin1@sure.com", Guid.NewGuid(), UserRole.Finance);
        user.SetFinanceApprover(true);

        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _repository.CountActiveFinanceApproversAsync(Arg.Any<CancellationToken>()).Returns(2);

        var command = new DeactivateUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserIsFinanceRoleButNotApprover_DeactivatesWithoutCheckingApproverCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("fin2", "Finance Regular", "fin2@sure.com", Guid.NewGuid(), UserRole.Finance);
        user.IsFinanceApprover.Should().BeFalse();

        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new DeactivateUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();

        await _repository.DidNotReceive().CountActiveFinanceApproversAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.DepartmentHead)]
    [InlineData(UserRole.Management)]
    [InlineData(UserRole.Admin)]
    public async Task Handle_WhenUserIsNotFinance_DeactivatesWithoutCheckingApproverCount(UserRole nonFinanceRole)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("normal", "Normal User", "normal@sure.com", Guid.NewGuid(), nonFinanceRole);

        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new DeactivateUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();

        await _repository.DidNotReceive().CountActiveFinanceApproversAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyInactive_IdempotentlyDeactivatesWithoutCheckingApprovers()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("fin", "Finance Approver", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);
        user.SetFinanceApprover(true);
        user.Deactivate();

        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new DeactivateUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();

        await _repository.DidNotReceive().CountActiveFinanceApproversAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("fin1", "Finance Approver", "fin1@sure.com", Guid.NewGuid(), UserRole.Finance);
        user.SetFinanceApprover(true);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _repository.GetByIdAsync(userId, ct).Returns(user);
        _repository.CountActiveFinanceApproversAsync(ct).Returns(3);

        var command = new DeactivateUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByIdAsync(userId, ct);
        await _repository.Received(1).CountActiveFinanceApproversAsync(ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
