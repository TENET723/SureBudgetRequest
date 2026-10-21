using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Security;
using SureBudgetRequest.Application.Users.Commands.CreateUser;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Users.Commands;

public class CreateUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IDepartmentRepository _departmentRepository = Substitute.For<IDepartmentRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandHandlerTests()
    {
        _handler = new CreateUserCommandHandler(
            _userRepository,
            _departmentRepository,
            _passwordHasher,
            _unitOfWork);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenEmailIsNullOrWhiteSpace_ReturnsEmailRequiredError(string? invalidEmail)
    {
        // Arrange
        var command = new CreateUserCommand(
            "johndoe",
            "John Doe",
            invalidEmail!,
            "ValidPass123!",
            Guid.NewGuid(),
            UserRole.Employee,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.EmailRequired);

        await _userRepository.DidNotReceive().EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    public async Task Handle_WhenPasswordIsShorterThanMinimum_ReturnsPasswordTooShortError(string? shortPassword)
    {
        // Arrange
        var command = new CreateUserCommand(
            "johndoe",
            "John Doe",
            "john@sure.com",
            shortPassword!,
            Guid.NewGuid(),
            UserRole.Employee,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.PasswordTooShort(CreateUserCommandHandler.MinimumPasswordLength));

        await _userRepository.DidNotReceive().EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.DepartmentHead)]
    [InlineData(UserRole.Management)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Accounting)]
    [InlineData(UserRole.Support)]
    public async Task Handle_WhenIsFinanceApproverTrueForNonFinanceRole_ReturnsOnlyFinanceCanBeApproverError(UserRole nonFinanceRole)
    {
        // Arrange
        var command = new CreateUserCommand(
            "testuser",
            "Test User",
            "test@sure.com",
            "ValidPass123!",
            Guid.NewGuid(),
            nonFinanceRole,
            null,
            IsFinanceApprover: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.OnlyFinanceCanBeApprover);

        await _userRepository.DidNotReceive().EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ReturnsEmailAlreadyExistsError()
    {
        // Arrange
        _userRepository.EmailExistsAsync("existing@sure.com", Arg.Any<CancellationToken>()).Returns(true);

        var command = new CreateUserCommand(
            "johndoe",
            "John Doe",
            "existing@sure.com",
            "ValidPass123!",
            Guid.NewGuid(),
            UserRole.Employee,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.EmailAlreadyExists);

        await _departmentRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDepartmentNotFound_ReturnsDepartmentNotFoundError()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        _userRepository.EmailExistsAsync("john@sure.com", Arg.Any<CancellationToken>()).Returns(false);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns((Department?)null);

        var command = new CreateUserCommand(
            "johndoe",
            "John Doe",
            "john@sure.com",
            "ValidPass123!",
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenUsernameIsNullOrWhiteSpace_ReturnsValidationError(string? invalidUsername)
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var dept = new Department("IT", null, 1000m, 5000m);
        _userRepository.EmailExistsAsync("john@sure.com", Arg.Any<CancellationToken>()).Returns(false);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);

        var command = new CreateUserCommand(
            invalidUsername!,
            "John Doe",
            "john@sure.com",
            "ValidPass123!",
            deptId,
            UserRole.Employee,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.Validation");
        result.Error.Message.Should().Contain("Username is required");

        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenFullNameIsNullOrWhiteSpace_ReturnsValidationError(string? invalidFullName)
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var dept = new Department("IT", null, 1000m, 5000m);
        _userRepository.EmailExistsAsync("john@sure.com", Arg.Any<CancellationToken>()).Returns(false);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);

        var command = new CreateUserCommand(
            "johndoe",
            invalidFullName!,
            "john@sure.com",
            "ValidPass123!",
            deptId,
            UserRole.Employee,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.Validation");
        result.Error.Message.Should().Contain("Full name is required");

        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValidEmployeeUser_CreatesUserWithHashedPasswordAndSaves()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var dept = new Department("IT", null, 1000m, 5000m);
        _userRepository.EmailExistsAsync("john@sure.com", Arg.Any<CancellationToken>()).Returns(false);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);
        _passwordHasher.Hash("Secret123!").Returns("hashed_secret_123");

        var generatedUserId = Guid.NewGuid();
        User? addedUser = null;
        _userRepository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                addedUser = callInfo.Arg<User>();
                typeof(User).GetProperty(nameof(User.Id))!.SetValue(addedUser, generatedUserId);
                return Task.CompletedTask;
            });

        var command = new CreateUserCommand(
            "johndoe",
            "John Doe",
            "john@sure.com",
            "Secret123!",
            deptId,
            UserRole.Employee,
            "U12345SLACK");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(generatedUserId);

        addedUser.Should().NotBeNull();
        addedUser!.Username.Should().Be("johndoe");
        addedUser.FullName.Should().Be("John Doe");
        addedUser.Email.Should().Be("john@sure.com");
        addedUser.Role.Should().Be(UserRole.Employee);
        addedUser.DepartmentId.Should().Be(deptId);
        addedUser.PasswordHash.Should().Be("hashed_secret_123");
        addedUser.MustChangePassword.Should().BeTrue();
        addedUser.SlackUserId.Should().Be("U12345SLACK");
        addedUser.IsFinanceApprover.Should().BeFalse();
        addedUser.IsActive.Should().BeTrue();

        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValidFinanceApproverUser_CreatesUserAndEnablesFinanceApprover()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var dept = new Department("FinanceDept", null, 1000m, 5000m);
        _userRepository.EmailExistsAsync("finuser@sure.com", Arg.Any<CancellationToken>()).Returns(false);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(dept);
        _passwordHasher.Hash("FinancePass123!").Returns("hashed_fin_pwd");

        var generatedUserId = Guid.NewGuid();
        User? addedUser = null;
        _userRepository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                addedUser = callInfo.Arg<User>();
                typeof(User).GetProperty(nameof(User.Id))!.SetValue(addedUser, generatedUserId);
                return Task.CompletedTask;
            });

        var command = new CreateUserCommand(
            "finadmin",
            "Finance Admin",
            "finuser@sure.com",
            "FinancePass123!",
            deptId,
            UserRole.Finance,
            null,
            IsFinanceApprover: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(generatedUserId);
        addedUser.Should().NotBeNull();
        addedUser!.Role.Should().Be(UserRole.Finance);
        addedUser.IsFinanceApprover.Should().BeTrue();

        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var dept = new Department("IT", null, 1000m, 5000m);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _userRepository.EmailExistsAsync("john@sure.com", ct).Returns(false);
        _departmentRepository.GetByIdAsync(deptId, ct).Returns(dept);
        _passwordHasher.Hash("Secret123!").Returns("hash");

        var command = new CreateUserCommand(
            "johndoe",
            "John Doe",
            "john@sure.com",
            "Secret123!",
            deptId,
            UserRole.Employee,
            null);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userRepository.Received(1).EmailExistsAsync("john@sure.com", ct);
        await _departmentRepository.Received(1).GetByIdAsync(deptId, ct);
        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
