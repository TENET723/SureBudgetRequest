using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.WithdrawMethods.Commands.ReactivateWithdrawMethod;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.WithdrawMethods.Commands;

public class ReactivateWithdrawMethodCommandHandlerTests
{
    private readonly IWithdrawMethodRepository _repository = Substitute.For<IWithdrawMethodRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ReactivateWithdrawMethodCommandHandler _handler;

    public ReactivateWithdrawMethodCommandHandlerTests()
    {
        _handler = new ReactivateWithdrawMethodCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenMethodNotFound_ReturnsGenericNotFoundError()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((WithdrawMethod?)null);

        var command = new ReactivateWithdrawMethodCommand(id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WithdrawMethodErrors.GenericNotFound);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMethodExistsAndInactive_ReactivatesAndSaves()
    {
        // Arrange
        var id = Guid.NewGuid();
        var method = new WithdrawMethod("Cheque");
        method.Deactivate();
        method.IsActive.Should().BeFalse();

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(method);

        var command = new ReactivateWithdrawMethodCommand(id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        method.IsActive.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMethodAlreadyActive_IdempotentlyReactivatesAndSaves()
    {
        // Arrange
        var id = Guid.NewGuid();
        var method = new WithdrawMethod("Cheque");
        method.IsActive.Should().BeTrue();

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(method);

        var command = new ReactivateWithdrawMethodCommand(id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        method.IsActive.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var id = Guid.NewGuid();
        var method = new WithdrawMethod("Cheque");
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _repository.GetByIdAsync(id, ct).Returns(method);

        var command = new ReactivateWithdrawMethodCommand(id);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByIdAsync(id, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
