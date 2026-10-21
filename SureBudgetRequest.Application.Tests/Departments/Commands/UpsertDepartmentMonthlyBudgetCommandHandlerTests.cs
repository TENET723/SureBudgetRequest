using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Departments.Commands.UpsertDepartmentMonthlyBudget;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Departments.Commands;

public class UpsertDepartmentMonthlyBudgetCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IDepartmentRepository _departmentRepository = Substitute.For<IDepartmentRepository>();
    private readonly IDepartmentMonthlyBudgetRepository _monthlyBudgetRepository = Substitute.For<IDepartmentMonthlyBudgetRepository>();
    private readonly IAppSettingRepository _appSettingRepository = Substitute.For<IAppSettingRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly UpsertDepartmentMonthlyBudgetCommandHandler _handler;

    public UpsertDepartmentMonthlyBudgetCommandHandlerTests()
    {
        _handler = new UpsertDepartmentMonthlyBudgetCommandHandler(
            _userRepository,
            _departmentRepository,
            _monthlyBudgetRepository,
            _appSettingRepository,
            _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenActorNotFound_ReturnsUserNotFoundError()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        _userRepository.GetByIdAsync(actorId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new UpsertDepartmentMonthlyBudgetCommand(
            Guid.NewGuid(),
            2026,
            new List<MonthAmountEntry>(),
            actorId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(actorId));

        await _departmentRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.DepartmentHead)]
    [InlineData(UserRole.Management)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Accounting)]
    [InlineData(UserRole.Support)]
    public async Task Handle_WhenActorRoleIsNotFinance_ReturnsForbiddenError(UserRole nonFinanceRole)
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var actor = new User("actor", "Actor Name", "actor@sure.com", Guid.NewGuid(), nonFinanceRole);
        _userRepository.GetByIdAsync(actorId, Arg.Any<CancellationToken>()).Returns(actor);

        var command = new UpsertDepartmentMonthlyBudgetCommand(
            Guid.NewGuid(),
            2026,
            new List<MonthAmountEntry>(),
            actorId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DepartmentErrors.Forbidden);

        await _departmentRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDepartmentNotFound_ReturnsDepartmentNotFoundError()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance User", "finance@sure.com", Guid.NewGuid(), UserRole.Finance);
        _userRepository.GetByIdAsync(actorId, Arg.Any<CancellationToken>()).Returns(financeUser);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns((Department?)null);

        var command = new UpsertDepartmentMonthlyBudgetCommand(
            deptId,
            2026,
            new List<MonthAmountEntry>(),
            actorId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DepartmentErrors.NotFound);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(99)]
    public async Task Handle_WhenMonthIsInvalid_SkipsSilently(int invalidMonth)
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance User", "finance@sure.com", Guid.NewGuid(), UserRole.Finance);
        var dept = new Department("IT", null, 1000m, 5000m);

        _userRepository.GetByIdAsync(actorId, Arg.Any<CancellationToken>()).Returns(financeUser);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);

        var command = new UpsertDepartmentMonthlyBudgetCommand(
            deptId,
            DateTime.UtcNow.Year + 2,
            new List<MonthAmountEntry> { new(invalidMonth, 500_000m) },
            actorId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _monthlyBudgetRepository.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _monthlyBudgetRepository.DidNotReceive().AddAsync(Arg.Any<DepartmentMonthlyBudget>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMonthIsInPast_SkipsSilently()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance User", "finance@sure.com", Guid.NewGuid(), UserRole.Finance);
        var dept = new Department("IT", null, 1000m, 5000m);

        _userRepository.GetByIdAsync(actorId, Arg.Any<CancellationToken>()).Returns(financeUser);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);

        // Year 2020 is completely in the past
        var command = new UpsertDepartmentMonthlyBudgetCommand(
            deptId,
            2020,
            new List<MonthAmountEntry>
            {
                new(4, 500_000m),
                new(5, 600_000m)
            },
            actorId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _monthlyBudgetRepository.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _monthlyBudgetRepository.DidNotReceive().AddAsync(Arg.Any<DepartmentMonthlyBudget>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAmountIsNull_SkipsSilently()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var futureYear = DateTime.UtcNow.Year + 2;
        var financeUser = new User("fin", "Finance User", "finance@sure.com", Guid.NewGuid(), UserRole.Finance);
        var dept = new Department("IT", null, 1000m, 5000m);

        _userRepository.GetByIdAsync(actorId, Arg.Any<CancellationToken>()).Returns(financeUser);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);

        var command = new UpsertDepartmentMonthlyBudgetCommand(
            deptId,
            futureYear,
            new List<MonthAmountEntry> { new(6, null) },
            actorId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _monthlyBudgetRepository.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _monthlyBudgetRepository.DidNotReceive().AddAsync(Arg.Any<DepartmentMonthlyBudget>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFutureMonthAndNoExistingBudget_AddsNewBudget()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var futureYear = DateTime.UtcNow.Year + 2;
        var financeUser = new User("fin", "Finance User", "finance@sure.com", Guid.NewGuid(), UserRole.Finance);
        var dept = new Department("IT", null, 1000m, 5000m);

        _userRepository.GetByIdAsync(actorId, Arg.Any<CancellationToken>()).Returns(financeUser);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);
        _monthlyBudgetRepository.GetAsync(deptId, futureYear, 5, Arg.Any<CancellationToken>()).Returns((DepartmentMonthlyBudget?)null);

        DepartmentMonthlyBudget? addedBudget = null;
        await _monthlyBudgetRepository.AddAsync(Arg.Do<DepartmentMonthlyBudget>(b => addedBudget = b), Arg.Any<CancellationToken>());

        var command = new UpsertDepartmentMonthlyBudgetCommand(
            deptId,
            futureYear,
            new List<MonthAmountEntry> { new(5, 7_500_000m) },
            actorId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        addedBudget.Should().NotBeNull();
        addedBudget!.DepartmentId.Should().Be(deptId);
        addedBudget.Year.Should().Be(futureYear);
        addedBudget.Month.Should().Be(5);
        addedBudget.Amount.Should().Be(7_500_000m);
        addedBudget.CreatedByUserId.Should().Be(actorId);

        await _monthlyBudgetRepository.Received(1).AddAsync(Arg.Any<DepartmentMonthlyBudget>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFutureMonthAndBudgetAlreadyExists_UpdatesExistingBudget()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var futureYear = DateTime.UtcNow.Year + 2;
        var financeUser = new User("fin", "Finance User", "finance@sure.com", Guid.NewGuid(), UserRole.Finance);
        var dept = new Department("IT", null, 1000m, 5000m);
        var existingBudget = new DepartmentMonthlyBudget(deptId, futureYear, 7, 3_000_000m, Guid.NewGuid());

        _userRepository.GetByIdAsync(actorId, Arg.Any<CancellationToken>()).Returns(financeUser);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);
        _monthlyBudgetRepository.GetAsync(deptId, futureYear, 7, Arg.Any<CancellationToken>()).Returns(existingBudget);

        var command = new UpsertDepartmentMonthlyBudgetCommand(
            deptId,
            futureYear,
            new List<MonthAmountEntry> { new(7, 9_000_000m) },
            actorId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingBudget.Amount.Should().Be(9_000_000m);
        existingBudget.UpdatedByUserId.Should().Be(actorId);
        existingBudget.UpdatedAt.Should().NotBeNull();

        await _monthlyBudgetRepository.DidNotReceive().AddAsync(Arg.Any<DepartmentMonthlyBudget>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithCustomFinancialYearStartMonth_CalculatesCalendarYearAccurately()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var baseYear = DateTime.UtcNow.Year + 2;
        var financeUser = new User("fin", "Finance User", "finance@sure.com", Guid.NewGuid(), UserRole.Finance);
        var dept = new Department("IT", null, 1000m, 5000m);
        var fySetting = new AppSetting("FinancialYear.StartMonth", "10"); // FY starts in October

        _userRepository.GetByIdAsync(actorId, Arg.Any<CancellationToken>()).Returns(financeUser);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);
        _appSettingRepository.GetByKeyAsync("FinancialYear.StartMonth", Arg.Any<CancellationToken>()).Returns(fySetting);

        var command = new UpsertDepartmentMonthlyBudgetCommand(
            deptId,
            baseYear,
            new List<MonthAmountEntry>
            {
                new(11, 2_000_000m), // Nov (>= 10, so calendar year = baseYear)
                new(2, 3_000_000m)   // Feb (< 10, so calendar year = baseYear + 1)
            },
            actorId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _monthlyBudgetRepository.Received(1).GetAsync(deptId, baseYear, 11, Arg.Any<CancellationToken>());
        await _monthlyBudgetRepository.Received(1).GetAsync(deptId, baseYear, 2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFinancialYearSettingIsMalformed_FallsBackToApril()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var baseYear = DateTime.UtcNow.Year + 2;
        var financeUser = new User("fin", "Finance User", "finance@sure.com", Guid.NewGuid(), UserRole.Finance);
        var dept = new Department("IT", null, 1000m, 5000m);
        var malformedSetting = new AppSetting("FinancialYear.StartMonth", "invalid_month_format");

        _userRepository.GetByIdAsync(actorId, Arg.Any<CancellationToken>()).Returns(financeUser);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);
        _appSettingRepository.GetByKeyAsync("FinancialYear.StartMonth", Arg.Any<CancellationToken>()).Returns(malformedSetting);

        var command = new UpsertDepartmentMonthlyBudgetCommand(
            deptId,
            baseYear,
            new List<MonthAmountEntry> { new(4, 1_000_000m) },
            actorId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _monthlyBudgetRepository.Received(1).GetAsync(deptId, baseYear, 4, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var futureYear = DateTime.UtcNow.Year + 2;
        var financeUser = new User("fin", "Finance User", "finance@sure.com", Guid.NewGuid(), UserRole.Finance);
        var dept = new Department("IT", null, 1000m, 5000m);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _userRepository.GetByIdAsync(actorId, ct).Returns(financeUser);
        _departmentRepository.GetByIdAsync(deptId, ct).Returns(dept);
        _monthlyBudgetRepository.GetAsync(deptId, futureYear, 8, ct).Returns((DepartmentMonthlyBudget?)null);

        var command = new UpsertDepartmentMonthlyBudgetCommand(
            deptId,
            futureYear,
            new List<MonthAmountEntry> { new(8, 1_000_000m) },
            actorId);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userRepository.Received(1).GetByIdAsync(actorId, ct);
        await _departmentRepository.Received(1).GetByIdAsync(deptId, ct);
        await _appSettingRepository.Received(1).GetByKeyAsync("FinancialYear.StartMonth", ct);
        await _monthlyBudgetRepository.Received(1).GetAsync(deptId, futureYear, 8, ct);
        await _monthlyBudgetRepository.Received(1).AddAsync(Arg.Any<DepartmentMonthlyBudget>(), ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
