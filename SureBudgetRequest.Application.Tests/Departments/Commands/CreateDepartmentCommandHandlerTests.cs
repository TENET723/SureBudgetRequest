using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Departments.Commands.CreateDepartment;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Departments.Commands;

public class CreateDepartmentCommandHandlerTests
{
    private readonly IDepartmentRepository _departmentRepository = Substitute.For<IDepartmentRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateDepartmentCommandHandler _handler;

    public CreateDepartmentCommandHandlerTests()
    {
        _handler = new CreateDepartmentCommandHandler(
            _departmentRepository,
            _userRepository,
            _unitOfWork);
    }

    [Fact]
    public async Task Handle_WithValidDataAndHeadUser_CreatesDepartmentAndReturnsId()
    {
        // Arrange
        var headId = Guid.NewGuid();
        var headUser = new User("depthead", "Dept Head", "head@sure.com", Guid.NewGuid(), UserRole.DepartmentHead);
        _userRepository.GetByIdAsync(headId, Arg.Any<CancellationToken>()).Returns(headUser);

        var generatedId = Guid.NewGuid();
        Department? savedDept = null;
        _departmentRepository.AddAsync(Arg.Any<Department>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                savedDept = callInfo.Arg<Department>();
                typeof(Department).GetProperty(nameof(Department.Id))!.SetValue(savedDept, generatedId);
                return Task.CompletedTask;
            });

        var command = new CreateDepartmentCommand(
            "Engineering",
            headId,
            5_000_000m,
            50_000_000m,
            "https://hooks.slack.com/services/eng");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(generatedId);
        savedDept.Should().NotBeNull();
        savedDept!.Name.Should().Be("Engineering");
        savedDept.HeadUserId.Should().Be(headId);
        savedDept.BudgetLimit.Should().Be(5_000_000m);
        savedDept.MonthlyLimit.Should().Be(50_000_000m);
        savedDept.SlackWebhookUrl.Should().Be("https://hooks.slack.com/services/eng");
        savedDept.IsActive.Should().BeTrue();

        await _departmentRepository.Received(1).AddAsync(Arg.Any<Department>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidDataAndNullHeadUser_CreatesDepartmentWithoutCheckingUserRepository()
    {
        // Arrange
        var command = new CreateDepartmentCommand(
            "Human Resources",
            null,
            2_000_000m,
            20_000_000m,
            null);

        var generatedId = Guid.NewGuid();
        Department? savedDept = null;
        _departmentRepository.AddAsync(Arg.Any<Department>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                savedDept = callInfo.Arg<Department>();
                typeof(Department).GetProperty(nameof(Department.Id))!.SetValue(savedDept, generatedId);
                return Task.CompletedTask;
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(generatedId);
        savedDept.Should().NotBeNull();
        savedDept!.Name.Should().Be("Human Resources");
        savedDept.HeadUserId.Should().BeNull();
        savedDept.BudgetLimit.Should().Be(2_000_000m);
        savedDept.MonthlyLimit.Should().Be(20_000_000m);
        savedDept.SlackWebhookUrl.Should().BeNull();

        await _userRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _departmentRepository.Received(1).AddAsync(Arg.Any<Department>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenHeadUserNotFound_ReturnsHeadNotFoundError()
    {
        // Arrange
        var headId = Guid.NewGuid();
        _userRepository.GetByIdAsync(headId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new CreateDepartmentCommand(
            "Operations",
            headId,
            1_000_000m,
            10_000_000m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DepartmentErrors.HeadNotFound);

        await _departmentRepository.DidNotReceive().AddAsync(Arg.Any<Department>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenNameIsNullOrWhiteSpace_ReturnsValidationError(string? invalidName)
    {
        // Arrange
        var command = new CreateDepartmentCommand(
            invalidName!,
            null,
            1_000_000m,
            10_000_000m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Department.Validation");
        result.Error.Message.Should().Contain("Department name is required");

        await _departmentRepository.DidNotReceive().AddAsync(Arg.Any<Department>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1)]
    [InlineData(-100_000)]
    public async Task Handle_WhenBudgetLimitIsNegative_ReturnsValidationError(decimal negativeBudget)
    {
        // Arrange
        var command = new CreateDepartmentCommand(
            "Finance",
            null,
            negativeBudget,
            10_000_000m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Department.Validation");
        result.Error.Message.Should().Contain("Budget limit cannot be negative");

        await _departmentRepository.DidNotReceive().AddAsync(Arg.Any<Department>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1)]
    [InlineData(-50_000)]
    public async Task Handle_WhenMonthlyLimitIsNegative_ReturnsValidationError(decimal negativeMonthlyLimit)
    {
        // Arrange
        var command = new CreateDepartmentCommand(
            "Finance",
            null,
            1_000_000m,
            negativeMonthlyLimit);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Department.Validation");
        result.Error.Message.Should().Contain("Monthly limit cannot be negative");

        await _departmentRepository.DidNotReceive().AddAsync(Arg.Any<Department>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepositories()
    {
        // Arrange
        var headId = Guid.NewGuid();
        var headUser = new User("depthead", "Dept Head", "head@sure.com", Guid.NewGuid(), UserRole.DepartmentHead);
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _userRepository.GetByIdAsync(headId, ct).Returns(headUser);

        var command = new CreateDepartmentCommand("Legal", headId, 100m, 1000m);

        // Act
        await _handler.Handle(command, ct);

        // Assert
        await _userRepository.Received(1).GetByIdAsync(headId, ct);
        await _departmentRepository.Received(1).AddAsync(Arg.Any<Department>(), ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
