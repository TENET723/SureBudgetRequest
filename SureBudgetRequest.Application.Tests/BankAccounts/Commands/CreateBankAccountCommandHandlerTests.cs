using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BankAccounts.Commands.CreateBankAccount;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BankAccounts.Commands;

public class CreateBankAccountCommandHandlerTests
{
    private readonly IBankAccountRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreateBankAccountCommandHandler _handler;

    public CreateBankAccountCommandHandlerTests()
    {
        _repository = Substitute.For<IBankAccountRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new CreateBankAccountCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldAddAccountAndReturnSuccessWithId()
    {
        // Arrange
        var command = new CreateBankAccountCommand("KBZ Bank", "0987654321", "Alice Doe");
        _repository.ListAsync(includeInactive: false, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<BankAccount>());

        var generatedId = Guid.NewGuid();
        _repository.AddAsync(Arg.Any<BankAccount>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var acct = callInfo.Arg<BankAccount>();
                typeof(BankAccount).GetProperty(nameof(BankAccount.Id))!.SetValue(acct, generatedId);
                return Task.CompletedTask;
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(generatedId);

        await _repository.Received(1).AddAsync(
            Arg.Is<BankAccount>(b =>
                b.BankName == "KBZ Bank" &&
                b.AccountNumber == "0987654321" &&
                b.AccountHolderName == "Alice Doe"),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceAccountNumber_ShouldSkipDuplicateCheckAndSucceed(string? emptyNumber)
    {
        // Arrange
        var command = new CreateBankAccountCommand("AYA Bank", emptyNumber, "Bob");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Soft check should NOT query the repository
        await _repository.DidNotReceive().ListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).AddAsync(Arg.Any<BankAccount>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ActiveDuplicateAccountNumber_CaseInsensitive_ShouldReturnConflictFailure()
    {
        // Arrange
        var existingAccount = new BankAccount("CB Bank", "ACC-12345", "Charlie");
        _repository.ListAsync(includeInactive: false, Arg.Any<CancellationToken>())
            .Returns(new[] { existingAccount });

        var command = new CreateBankAccountCommand("CB Bank New Branch", "  acc-12345  ", "Charlie Duplicate");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BankAccount.AlreadyExists");
        result.Error.Message.Should().Be("An active bank account with number 'acc-12345' already exists.");

        await _repository.DidNotReceive().AddAsync(Arg.Any<BankAccount>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceBankName_ShouldCatchDomainExceptionAndReturnFailure(string? invalidBankName)
    {
        // Arrange
        var command = new CreateBankAccountCommand(invalidBankName!, "999999", "Holder");
        _repository.ListAsync(includeInactive: false, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<BankAccount>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Bank name is required.");

        await _repository.DidNotReceive().AddAsync(Arg.Any<BankAccount>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancellationToken_ShouldBePropagatedToDependencies()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var command = new CreateBankAccountCommand("UAB Bank", "112233", "Holder");

        _repository.ListAsync(includeInactive: false, ct).Returns(Array.Empty<BankAccount>());

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).ListAsync(includeInactive: false, ct);
        await _repository.Received(1).AddAsync(Arg.Any<BankAccount>(), ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
