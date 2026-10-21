using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BankAccounts.Commands.UpdateBankAccount;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BankAccounts.Commands;

public class UpdateBankAccountCommandHandlerTests
{
    private readonly IBankAccountRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateBankAccountCommandHandler _handler;

    public UpdateBankAccountCommandHandlerTests()
    {
        _repository = Substitute.For<IBankAccountRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new UpdateBankAccountCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_AccountExists_ActiveTrue_ShouldUpdateAndReactivate()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new BankAccount("Old Bank", "111", "Old Holder");
        account.Deactivate();
        account.IsActive.Should().BeFalse();

        _repository.GetByIdAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(account);

        var command = new UpdateBankAccountCommand(
            accountId, "New Bank", "222", "New Holder", IsActive: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        account.BankName.Should().Be("New Bank");
        account.AccountNumber.Should().Be("222");
        account.AccountHolderName.Should().Be("New Holder");
        account.IsActive.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AccountExists_ActiveFalse_ShouldUpdateAndDeactivate()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new BankAccount("KBZ", "12345", "Alice");
        account.IsActive.Should().BeTrue();

        _repository.GetByIdAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(account);

        var command = new UpdateBankAccountCommand(
            accountId, "KBZ Updated", "12345-Updated", "Alice Doe", IsActive: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        account.BankName.Should().Be("KBZ Updated");
        account.AccountNumber.Should().Be("12345-Updated");
        account.AccountHolderName.Should().Be("Alice Doe");
        account.IsActive.Should().BeFalse();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AccountNotFound_ShouldReturnGenericNotFoundFailure()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        _repository.GetByIdAsync(accountId, Arg.Any<CancellationToken>())
            .Returns((BankAccount?)null);

        var command = new UpdateBankAccountCommand(
            accountId, "Any Bank", "123", "Any Holder", IsActive: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BankAccountErrors.GenericNotFound.Code);
        result.Error.Message.Should().Be(BankAccountErrors.GenericNotFound.Message);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceBankName_ShouldCatchDomainExceptionAndReturnValidationError(string? invalidBankName)
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new BankAccount("Valid Bank", "123", "Holder");

        _repository.GetByIdAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(account);

        var command = new UpdateBankAccountCommand(
            accountId, invalidBankName!, "123", "Holder", IsActive: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BankAccount.Validation");
        result.Error.Message.Should().Contain("Bank name is required.");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancellationToken_ShouldBePropagatedToDependencies()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var accountId = Guid.NewGuid();
        var account = new BankAccount("Bank", "123", "Holder");

        _repository.GetByIdAsync(accountId, ct).Returns(account);

        var command = new UpdateBankAccountCommand(accountId, "New Bank", "123", null, true);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByIdAsync(accountId, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
