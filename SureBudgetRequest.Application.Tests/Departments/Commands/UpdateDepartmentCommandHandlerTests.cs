using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Departments.Commands.UpdateDepartment;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Departments.Commands;

public class UpdateDepartmentCommandHandlerTests
{
    private readonly IDepartmentRepository _departmentRepository = Substitute.For<IDepartmentRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly UpdateDepartmentCommandHandler _handler;

    public UpdateDepartmentCommandHandlerTests()
    {
        _handler = new UpdateDepartmentCommandHandler(
            _departmentRepository,
            _userRepository,
            _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenDepartmentNotFound_ReturnsDepartmentNotFoundError()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns((Department?)null);

        var command = new UpdateDepartmentCommand(deptId, "New Name", null, 1000m, 5000m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DepartmentErrors.NotFound);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenHeadUserProvidedButNotFound_ReturnsHeadNotFoundError()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var dept = new Department("Engineering", null, 1000m, 5000m);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.GetByIdAsync(headId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new UpdateDepartmentCommand(deptId, "New Name", headId, 2000m, 10000m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DepartmentErrors.HeadNotFound);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValidWithHeadUser_UpdatesAllFieldsAndSaves()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var headUser = new User("depthead", "Dept Head", "head@sure.com", Guid.NewGuid(), UserRole.DepartmentHead);
        var dept = new Department("Old Name", null, 1000m, 5000m);

        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.GetByIdAsync(headId, Arg.Any<CancellationToken>()).Returns(headUser);

        var command = new UpdateDepartmentCommand(
            deptId,
            "Engineering & DevOps",
            headId,
            10_000_000m,
            80_000_000m,
            "https://hooks.slack.com/services/T00/B00/XXXX");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        dept.Name.Should().Be("Engineering & DevOps");
        dept.HeadUserId.Should().Be(headId);
        dept.BudgetLimit.Should().Be(10_000_000m);
        dept.MonthlyLimit.Should().Be(80_000_000m);
        dept.SlackWebhookUrl.Should().Be("https://hooks.slack.com/services/T00/B00/XXXX");

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValidWithNullHeadUser_ClearsHeadUserAndSavesWithoutCheckingUserRepository()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var initialHeadId = Guid.NewGuid();
        var dept = new Department("Operations", initialHeadId, 2000m, 10000m);

        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);

        var command = new UpdateDepartmentCommand(
            deptId,
            "Operations Vacant",
            null,
            3000m,
            15000m,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        dept.Name.Should().Be("Operations Vacant");
        dept.HeadUserId.Should().BeNull();
        dept.BudgetLimit.Should().Be(3000m);
        dept.MonthlyLimit.Should().Be(15000m);
        dept.SlackWebhookUrl.Should().BeNull();

        await _userRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenNameIsInvalid_ReturnsValidationError(string? invalidName)
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var dept = new Department("Original", null, 1000m, 5000m);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);

        var command = new UpdateDepartmentCommand(deptId, invalidName!, null, 1000m, 5000m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Department.Validation");
        result.Error.Message.Should().Contain("Department name is required");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public async Task Handle_WhenBudgetLimitIsNegative_ReturnsValidationError(decimal negativeBudget)
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var dept = new Department("Original", null, 1000m, 5000m);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);

        var command = new UpdateDepartmentCommand(deptId, "Valid Name", null, negativeBudget, 5000m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Department.Validation");
        result.Error.Message.Should().Contain("Budget limit cannot be negative");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1)]
    [InlineData(-5000)]
    public async Task Handle_WhenMonthlyLimitIsNegative_ReturnsValidationError(decimal negativeMonthly)
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var dept = new Department("Original", null, 1000m, 5000m);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);

        var command = new UpdateDepartmentCommand(deptId, "Valid Name", null, 1000m, negativeMonthly);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Department.Validation");
        result.Error.Message.Should().Contain("Monthly limit cannot be negative");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("https://discord.com/api/webhooks/xxx")]
    [InlineData("http://hooks.slack.com/services/xxx")]
    [InlineData("ftp://hooks.slack.com/services/xxx")]
    public async Task Handle_WhenSlackWebhookUrlIsInvalid_ReturnsValidationError(string invalidUrl)
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var dept = new Department("Original", null, 1000m, 5000m);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);

        var command = new UpdateDepartmentCommand(deptId, "Valid Name", null, 1000m, 5000m, invalidUrl);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Department.Validation");
        result.Error.Message.Should().Contain("Slack webhook URL must start with 'https://hooks.slack.com/services/'");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var headUser = new User("depthead", "Dept Head", "head@sure.com", Guid.NewGuid(), UserRole.DepartmentHead);
        var dept = new Department("Original", null, 1000m, 5000m);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _departmentRepository.GetByIdAsync(deptId, ct).Returns(dept);
        _userRepository.GetByIdAsync(headId, ct).Returns(headUser);

        var command = new UpdateDepartmentCommand(deptId, "New Name", headId, 2000m, 10000m);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _departmentRepository.Received(1).GetByIdAsync(deptId, ct);
        await _userRepository.Received(1).GetByIdAsync(headId, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
