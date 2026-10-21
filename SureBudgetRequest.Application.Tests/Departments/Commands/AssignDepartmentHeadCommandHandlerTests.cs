using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Departments.Commands.AssignDepartmentHead;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Departments.Commands;

public class AssignDepartmentHeadCommandHandlerTests
{
    private readonly IDepartmentRepository _departmentRepository = Substitute.For<IDepartmentRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AssignDepartmentHeadCommandHandler _handler;

    public AssignDepartmentHeadCommandHandlerTests()
    {
        _handler = new AssignDepartmentHeadCommandHandler(
            _departmentRepository,
            _userRepository,
            _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenDepartmentNotFound_ReturnsDepartmentNotFoundError()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns((Department?)null);

        var command = new AssignDepartmentHeadCommand(deptId, headId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DepartmentErrors.NotFound);

        await _userRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenHeadUserNotFound_ReturnsUserNotFoundOrInactiveError()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var dept = new Department("IT", null, 1000m, 5000m);

        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.GetByIdAsync(headId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new AssignDepartmentHeadCommand(deptId, headId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFoundOrInactive);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenHeadUserIsInactive_ReturnsUserNotFoundOrInactiveError()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var dept = new Department("IT", null, 1000m, 5000m);
        var headUser = new User("inactivehead", "Inactive Head", "inactive@sure.com", Guid.NewGuid(), UserRole.DepartmentHead);
        headUser.Deactivate();

        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.GetByIdAsync(headId, Arg.Any<CancellationToken>()).Returns(headUser);

        var command = new AssignDepartmentHeadCommand(deptId, headId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFoundOrInactive);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDepartmentAndHeadUserAreValid_AssignsHeadAndSavesChanges()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var dept = new Department("IT", null, 1000m, 5000m);
        var headUser = new User("validhead", "Valid Head", "head@sure.com", Guid.NewGuid(), UserRole.DepartmentHead);

        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.GetByIdAsync(headId, Arg.Any<CancellationToken>()).Returns(headUser);

        var command = new AssignDepartmentHeadCommand(deptId, headId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        dept.HeadUserId.Should().Be(headId);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var dept = new Department("IT", null, 1000m, 5000m);
        var headUser = new User("validhead", "Valid Head", "head@sure.com", Guid.NewGuid(), UserRole.DepartmentHead);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _departmentRepository.GetByIdAsync(deptId, ct).Returns(dept);
        _userRepository.GetByIdAsync(headId, ct).Returns(headUser);

        var command = new AssignDepartmentHeadCommand(deptId, headId);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _departmentRepository.Received(1).GetByIdAsync(deptId, ct);
        await _userRepository.Received(1).GetByIdAsync(headId, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
