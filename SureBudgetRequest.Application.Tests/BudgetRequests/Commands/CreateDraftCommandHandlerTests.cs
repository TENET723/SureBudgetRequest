using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.CreateDraft;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class CreateDraftCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICurrencyRepository _currencyRepository = Substitute.For<ICurrencyRepository>();
    private readonly IWithdrawMethodRepository _withdrawMethodRepository = Substitute.For<IWithdrawMethodRepository>();
    private readonly IBudgetCategoryRepository _budgetCategoryRepository = Substitute.For<IBudgetCategoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateDraftCommandHandler _handler;

    public CreateDraftCommandHandlerTests()
    {
        _handler = new CreateDraftCommandHandler(
            _budgetRequestRepository,
            _userRepository,
            _currencyRepository,
            _withdrawMethodRepository,
            _budgetCategoryRepository,
            _unitOfWork);
    }

    private static CreateDraftCommand CreateValidCommand(
        Guid requesterId,
        Guid withdrawMethodId,
        Guid? categoryId = null,
        string currencyCode = "MMK")
    {
        return new CreateDraftCommand(
            requesterId,
            Guid.NewGuid(),
            BudgetRequestType.Standard,
            "Bob Head",
            DateTime.UtcNow.AddDays(2),
            50_000m,
            currencyCode,
            "Team lunch",
            "Alice",
            "Software Engineer",
            withdrawMethodId,
            false,
            null,
            null,
            null,
            categoryId);
    }

    [Fact]
    public async Task Handle_WhenWithdrawMethodIdIsEmpty_ReturnsWithdrawMethodRequiredError()
    {
        // Arrange
        var command = CreateValidCommand(Guid.NewGuid(), Guid.Empty);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WithdrawMethodErrors.Required);

        await _userRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequesterNotFound_ReturnsUserNotFoundError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = CreateValidCommand(requesterId, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(requesterId));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequesterIsInactive_ReturnsUserInactiveError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var requester = new User("inactive", "Inactive User", "inactive@sure.com", Guid.NewGuid(), UserRole.Employee);
        requester.Deactivate();

        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);

        var command = CreateValidCommand(requesterId, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Inactive);

        await _currencyRepository.DidNotReceive().GetByCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCurrencyNotFound_ReturnsCurrencyNotFoundError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var requester = new User("alice", "Alice", "alice@sure.com", Guid.NewGuid(), UserRole.Employee);
        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _currencyRepository.GetByCodeAsync("USD", Arg.Any<CancellationToken>()).Returns((Currency?)null);

        var command = CreateValidCommand(requesterId, Guid.NewGuid(), currencyCode: "USD");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CurrencyErrors.NotFound("USD"));

        await _withdrawMethodRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCurrencyIsInactive_ReturnsCurrencyInactiveError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var requester = new User("alice", "Alice", "alice@sure.com", Guid.NewGuid(), UserRole.Employee);
        var currency = new Currency("THB", "Thai Baht", 100m);
        currency.Deactivate();

        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _currencyRepository.GetByCodeAsync("THB", Arg.Any<CancellationToken>()).Returns(currency);

        var command = CreateValidCommand(requesterId, Guid.NewGuid(), currencyCode: "THB");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CurrencyErrors.Inactive("THB"));

        await _withdrawMethodRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenWithdrawMethodNotFound_ReturnsWithdrawMethodNotFoundError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var withdrawMethodId = Guid.NewGuid();
        var requester = new User("alice", "Alice", "alice@sure.com", Guid.NewGuid(), UserRole.Employee);
        var currency = new Currency("MMK", "Myanmar Kyat", 1m);

        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(currency);
        _withdrawMethodRepository.GetByIdAsync(withdrawMethodId, Arg.Any<CancellationToken>()).Returns((WithdrawMethod?)null);

        var command = CreateValidCommand(requesterId, withdrawMethodId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WithdrawMethodErrors.NotFound);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenWithdrawMethodIsInactive_ReturnsWithdrawMethodInactiveError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var withdrawMethodId = Guid.NewGuid();
        var requester = new User("alice", "Alice", "alice@sure.com", Guid.NewGuid(), UserRole.Employee);
        var currency = new Currency("MMK", "Myanmar Kyat", 1m);
        var method = new WithdrawMethod("Old Cheque");
        method.Deactivate();

        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(currency);
        _withdrawMethodRepository.GetByIdAsync(withdrawMethodId, Arg.Any<CancellationToken>()).Returns(method);

        var command = CreateValidCommand(requesterId, withdrawMethodId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WithdrawMethodErrors.Inactive("Old Cheque"));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenBudgetCategoryIdProvidedButNotFound_ReturnsBudgetCategoryNotFoundError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var withdrawMethodId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var requester = new User("alice", "Alice", "alice@sure.com", Guid.NewGuid(), UserRole.Employee);
        var currency = new Currency("MMK", "Myanmar Kyat", 1m);
        var method = new WithdrawMethod("Cash");

        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(currency);
        _withdrawMethodRepository.GetByIdAsync(withdrawMethodId, Arg.Any<CancellationToken>()).Returns(method);
        _budgetCategoryRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>()).Returns((BudgetCategory?)null);

        var command = CreateValidCommand(requesterId, withdrawMethodId, categoryId: categoryId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetCategoryErrors.NotFound);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenBudgetCategoryIdProvidedButInactive_ReturnsBudgetCategoryInactiveError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var withdrawMethodId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var requester = new User("alice", "Alice", "alice@sure.com", Guid.NewGuid(), UserRole.Employee);
        var currency = new Currency("MMK", "Myanmar Kyat", 1m);
        var method = new WithdrawMethod("Cash");
        var category = new BudgetCategory("Deprecating");
        category.Deactivate();

        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(currency);
        _withdrawMethodRepository.GetByIdAsync(withdrawMethodId, Arg.Any<CancellationToken>()).Returns(method);
        _budgetCategoryRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>()).Returns(category);

        var command = CreateValidCommand(requesterId, withdrawMethodId, categoryId: categoryId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetCategoryErrors.Inactive("Deprecating"));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenDomainThrowsArgumentException_ReturnsValidationError(string invalidReasons)
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var withdrawMethodId = Guid.NewGuid();
        var requester = new User("alice", "Alice", "alice@sure.com", Guid.NewGuid(), UserRole.Employee);
        var currency = new Currency("MMK", "Myanmar Kyat", 1m);
        var method = new WithdrawMethod("Cash");

        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(currency);
        _withdrawMethodRepository.GetByIdAsync(withdrawMethodId, Arg.Any<CancellationToken>()).Returns(method);

        var command = new CreateDraftCommand(
            requesterId,
            Guid.NewGuid(),
            BudgetRequestType.Standard,
            "Bob Head",
            DateTime.UtcNow.AddDays(2),
            50_000m,
            "MMK",
            invalidReasons, // invalid empty reasons!
            "Alice",
            "Software Engineer",
            withdrawMethodId,
            false,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BudgetRequest.CreateError");

        await _budgetRequestRepository.DidNotReceive().AddAsync(Arg.Any<BudgetRequest>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValid_CreatesDraftSavesAndReturnsId()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var withdrawMethodId = Guid.NewGuid();
        var requester = new User("alice", "Alice Doe", "alice@sure.com", Guid.NewGuid(), UserRole.Employee);
        var currency = new Currency("MMK", "Myanmar Kyat", 1m);
        var method = new WithdrawMethod("Bank Transfer");

        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(currency);
        _withdrawMethodRepository.GetByIdAsync(withdrawMethodId, Arg.Any<CancellationToken>()).Returns(method);

        var generatedId = Guid.NewGuid();
        BudgetRequest? addedDraft = null;
        await _budgetRequestRepository.AddAsync(Arg.Do<BudgetRequest>(d =>
        {
            addedDraft = d;
            typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(addedDraft, generatedId);
        }), Arg.Any<CancellationToken>());

        var command = CreateValidCommand(requesterId, withdrawMethodId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(generatedId);

        addedDraft.Should().NotBeNull();
        addedDraft!.Status.Should().Be(RequestStatus.Draft);
        addedDraft.RequesterId.Should().Be(requesterId);
        addedDraft.CurrencyCode.Should().Be("MMK");
        addedDraft.RequestedAmount.Should().Be(50_000m);

        await _budgetRequestRepository.Received(1).AddAsync(Arg.Any<BudgetRequest>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var withdrawMethodId = Guid.NewGuid();
        var requester = new User("alice", "Alice Doe", "alice@sure.com", Guid.NewGuid(), UserRole.Employee);
        var currency = new Currency("MMK", "Myanmar Kyat", 1m);
        var method = new WithdrawMethod("Bank Transfer");

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _userRepository.GetByIdAsync(requesterId, ct).Returns(requester);
        _currencyRepository.GetByCodeAsync("MMK", ct).Returns(currency);
        _withdrawMethodRepository.GetByIdAsync(withdrawMethodId, ct).Returns(method);

        var command = CreateValidCommand(requesterId, withdrawMethodId);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userRepository.Received(1).GetByIdAsync(requesterId, ct);
        await _currencyRepository.Received(1).GetByCodeAsync("MMK", ct);
        await _withdrawMethodRepository.Received(1).GetByIdAsync(withdrawMethodId, ct);
        await _budgetRequestRepository.Received(1).AddAsync(Arg.Any<BudgetRequest>(), ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
