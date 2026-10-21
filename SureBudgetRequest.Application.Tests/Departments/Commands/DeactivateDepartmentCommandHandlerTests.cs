using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Departments.Commands.DeactivateDepartment;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Departments.Commands;

public class DeactivateDepartmentCommandHandlerTests
{
    private readonly IDepartmentRepository _repository = Substitute.For<IDepartmentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DeactivateDepartmentCommandHandler _handler;

    public DeactivateDepartmentCommandHandlerTests()
    {
        _handler = new DeactivateDepartmentCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenDepartmentNotFound_ReturnsDepartmentNotFoundError()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        _repository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns((Department?)null);

        var command = new DeactivateDepartmentCommand(deptId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DepartmentErrors.NotFound);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDepartmentExists_DeactivatesDepartmentAndSaves()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var dept = new Department("Marketing", null, 1000m, 5000m);
        dept.IsActive.Should().BeTrue();

        _repository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);

        var command = new DeactivateDepartmentCommand(deptId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        dept.IsActive.Should().BeFalse();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDepartmentAlreadyInactive_IdempotentlyDeactivatesAndSaves()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var dept = new Department("Marketing", null, 1000m, 5000m);
        dept.Deactivate();

        _repository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);

        var command = new DeactivateDepartmentCommand(deptId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        dept.IsActive.Should().BeFalse();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var dept = new Department("Marketing", null, 1000m, 5000m);
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _repository.GetByIdAsync(deptId, ct).Returns(dept);

        var command = new DeactivateDepartmentCommand(deptId);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByIdAsync(deptId, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
