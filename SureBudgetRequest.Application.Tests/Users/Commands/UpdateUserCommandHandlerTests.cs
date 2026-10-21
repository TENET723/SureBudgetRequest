using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Users.Commands.UpdateUser;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Users.Commands;

public class UpdateUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IDepartmentRepository _departmentRepository = Substitute.For<IDepartmentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly UpdateUserCommandHandler _handler;

    public UpdateUserCommandHandlerTests()
    {
        _handler = new UpdateUserCommandHandler(
            _userRepository,
            _departmentRepository,
            _unitOfWork);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenEmailIsNullOrWhiteSpace_ReturnsEmailRequiredError(string? invalidEmail)
    {
        // Arrange
        var command = new UpdateUserCommand(
            Guid.NewGuid(),
            "John Doe",
            invalidEmail!,
            Guid.NewGuid(),
            UserRole.Employee,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.EmailRequired);

        await _userRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsGenericNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new UpdateUserCommand(
            userId,
            "John Doe",
            "john@sure.com",
            Guid.NewGuid(),
            UserRole.Employee,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.GenericNotFound);

        await _departmentRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDepartmentNotFound_ReturnsDepartmentNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var user = new User("john", "John Doe", "john@sure.com", Guid.NewGuid(), UserRole.Employee);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns((Department?)null);

        var command = new UpdateUserCommand(
            userId,
            "John Doe",
            "john@sure.com",
            deptId,
            UserRole.Employee,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DepartmentErrors.NotFound);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEmailChangedAndAlreadyTaken_ReturnsEmailAlreadyExistsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var user = new User("john", "John Doe", "oldemail@sure.com", deptId, UserRole.Employee);
        var dept = new Department("IT", null, 1000m, 5000m);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.EmailExistsAsync("newemail@sure.com", Arg.Any<CancellationToken>()).Returns(true);

        var command = new UpdateUserCommand(
            userId,
            "John Doe",
            "newemail@sure.com",
            deptId,
            UserRole.Employee,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.EmailAlreadyExists);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEmailUnchanged_DoesNotQueryEmailExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var user = new User("john", "John Doe", "john@sure.com", deptId, UserRole.Employee);
        var dept = new Department("IT", null, 1000m, 5000m);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);

        var command = new UpdateUserCommand(
            userId,
            "John Updated",
            "JOHN@SURE.COM  ", // same normalized email
            deptId,
            UserRole.Employee,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userRepository.DidNotReceive().EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.DepartmentHead)]
    [InlineData(UserRole.Management)]
    [InlineData(UserRole.Admin)]
    public async Task Handle_WhenIsFinanceApproverTrueForNonFinanceRole_ReturnsOnlyFinanceCanBeApproverError(UserRole nonFinanceRole)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var user = new User("john", "John Doe", "john@sure.com", deptId, UserRole.Employee);
        var dept = new Department("IT", null, 1000m, 5000m);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);

        var command = new UpdateUserCommand(
            userId,
            "John Doe",
            "john@sure.com",
            deptId,
            nonFinanceRole,
            null,
            IsFinanceApprover: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.OnlyFinanceCanBeApprover);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDemotingLastActiveFinanceApproverByRole_ReturnsLastActiveFinanceApproverError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var user = new User("fin1", "Finance 1", "fin1@sure.com", deptId, UserRole.Finance);
        user.SetFinanceApprover(true);
        var dept = new Department("FinanceDept", null, 1000m, 5000m);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.CountActiveFinanceApproversAsync(Arg.Any<CancellationToken>()).Returns(1);

        var command = new UpdateUserCommand(
            userId,
            "Finance 1 Demoted",
            "fin1@sure.com",
            deptId,
            UserRole.Employee,
            null,
            IsFinanceApprover: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.LastActiveFinanceApprover);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRemovingApproverFlagFromLastActiveFinanceApprover_ReturnsLastActiveFinanceApproverError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var user = new User("fin1", "Finance 1", "fin1@sure.com", deptId, UserRole.Finance);
        user.SetFinanceApprover(true);
        var dept = new Department("FinanceDept", null, 1000m, 5000m);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.CountActiveFinanceApproversAsync(Arg.Any<CancellationToken>()).Returns(1);

        var command = new UpdateUserCommand(
            userId,
            "Finance 1",
            "fin1@sure.com",
            deptId,
            UserRole.Finance,
            null,
            IsFinanceApprover: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.LastActiveFinanceApprover);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenStrippingApproverRightsAndOtherApproversExist_UpdatesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var user = new User("fin1", "Finance 1", "fin1@sure.com", deptId, UserRole.Finance);
        user.SetFinanceApprover(true);
        var dept = new Department("FinanceDept", null, 1000m, 5000m);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.CountActiveFinanceApproversAsync(Arg.Any<CancellationToken>()).Returns(2);

        var command = new UpdateUserCommand(
            userId,
            "Finance 1",
            "fin1@sure.com",
            deptId,
            UserRole.Finance,
            null,
            IsFinanceApprover: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsFinanceApprover.Should().BeFalse();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserWasNotApprover_DoesNotCheckActiveApproverCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var user = new User("emp", "Employee", "emp@sure.com", deptId, UserRole.Employee);
        var dept = new Department("IT", null, 1000m, 5000m);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);

        var command = new UpdateUserCommand(
            userId,
            "Employee Renamed",
            "emp@sure.com",
            deptId,
            UserRole.DepartmentHead,
            null,
            IsFinanceApprover: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userRepository.DidNotReceive().CountActiveFinanceApproversAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValidUpdate_UpdatesAllUserFieldsAndSaves()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var initialDeptId = Guid.NewGuid();
        var targetDeptId = Guid.NewGuid();
        var user = new User("janedoe", "Jane Doe", "jane@old.com", initialDeptId, UserRole.Employee);
        var targetDept = new Department("New Dept", null, 1000m, 5000m);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _departmentRepository.GetByIdAsync(targetDeptId, Arg.Any<CancellationToken>()).Returns(targetDept);
        _userRepository.EmailExistsAsync("jane@new.com", Arg.Any<CancellationToken>()).Returns(false);

        var command = new UpdateUserCommand(
            userId,
            "Jane Smith",
            "jane@new.com",
            targetDeptId,
            UserRole.Finance,
            "U98765SLACK",
            IsFinanceApprover: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.FullName.Should().Be("Jane Smith");
        user.Email.Should().Be("jane@new.com");
        user.DepartmentId.Should().Be(targetDeptId);
        user.Role.Should().Be(UserRole.Finance);
        user.SlackUserId.Should().Be("U98765SLACK");
        user.IsFinanceApprover.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var user = new User("john", "John Doe", "john@sure.com", deptId, UserRole.Employee);
        var dept = new Department("IT", null, 1000m, 5000m);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _userRepository.GetByIdAsync(userId, ct).Returns(user);
        _departmentRepository.GetByIdAsync(deptId, ct).Returns(dept);

        var command = new UpdateUserCommand(
            userId,
            "John Doe",
            "john@sure.com",
            deptId,
            UserRole.Employee,
            null);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userRepository.Received(1).GetByIdAsync(userId, ct);
        await _departmentRepository.Received(1).GetByIdAsync(deptId, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
