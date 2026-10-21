using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BankAccounts.Commands.DeactivateBankAccount;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BankAccounts.Commands;

public class DeactivateBankAccountCommandHandlerTests
{
    private readonly IBankAccountRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DeactivateBankAccountCommandHandler _handler;

    public DeactivateBankAccountCommandHandlerTests()
    {
        _repository = Substitute.For<IBankAccountRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new DeactivateBankAccountCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_AccountExists_ShouldDeactivateAndReturnSuccess()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new BankAccount("KBZ Bank", "12345", "Alice");
        account.IsActive.Should().BeTrue();

        _repository.GetByIdAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(account);

        var command = new DeactivateBankAccountCommand(accountId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        account.IsActive.Should().BeFalse();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AccountAlreadyInactive_ShouldRemainInactiveAndReturnSuccess()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new BankAccount("AYA Bank", "67890", "Bob");
        account.Deactivate();
        account.IsActive.Should().BeFalse();

        _repository.GetByIdAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(account);

        var command = new DeactivateBankAccountCommand(accountId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
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

        var command = new DeactivateBankAccountCommand(accountId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BankAccountErrors.GenericNotFound.Code);
        result.Error.Message.Should().Be(BankAccountErrors.GenericNotFound.Message);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancellationToken_ShouldBePropagatedToDependencies()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var accountId = Guid.NewGuid();
        var account = new BankAccount("CB Bank", "111", "Charlie");

        _repository.GetByIdAsync(accountId, ct).Returns(account);

        var command = new DeactivateBankAccountCommand(accountId);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByIdAsync(accountId, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
