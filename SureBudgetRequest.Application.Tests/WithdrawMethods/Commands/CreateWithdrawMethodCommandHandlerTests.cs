using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.WithdrawMethods.Commands.CreateWithdrawMethod;
using SureBudgetRequest.Domain.Entities;
using Xunit;

namespace SureBudgetRequest.Application.Tests.WithdrawMethods.Commands;

public class CreateWithdrawMethodCommandHandlerTests
{
    private readonly IWithdrawMethodRepository _repository = Substitute.For<IWithdrawMethodRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateWithdrawMethodCommandHandler _handler;

    public CreateWithdrawMethodCommandHandlerTests()
    {
        _handler = new CreateWithdrawMethodCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenMethodWithSameNameAlreadyExists_ReturnsDuplicateFailure()
    {
        // Arrange
        var existing = new WithdrawMethod("Bank Transfer", requiresAttachment: true);
        _repository.GetByNameAsync("Bank Transfer", Arg.Any<CancellationToken>()).Returns(existing);

        var command = new CreateWithdrawMethodCommand("Bank Transfer", RequiresAttachment: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Withdraw method 'Bank Transfer' already exists.");

        await _repository.DidNotReceive().AddAsync(Arg.Any<WithdrawMethod>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenNameIsNullOrWhiteSpace_ReturnsValidationFailure(string? invalidName)
    {
        // Arrange
        _repository.GetByNameAsync(invalidName!, Arg.Any<CancellationToken>()).Returns((WithdrawMethod?)null);

        var command = new CreateWithdrawMethodCommand(invalidName!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Name is required");

        await _repository.DidNotReceive().AddAsync(Arg.Any<WithdrawMethod>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_WhenValid_AddsMethodAndReturnsId(bool requiresAttachment)
    {
        // Arrange
        _repository.GetByNameAsync("Cash In Hand", Arg.Any<CancellationToken>()).Returns((WithdrawMethod?)null);

        var generatedId = Guid.NewGuid();
        WithdrawMethod? savedMethod = null;
        _repository.AddAsync(Arg.Any<WithdrawMethod>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                savedMethod = callInfo.Arg<WithdrawMethod>();
                typeof(WithdrawMethod).GetProperty(nameof(WithdrawMethod.Id))!.SetValue(savedMethod, generatedId);
                return Task.CompletedTask;
            });

        var command = new CreateWithdrawMethodCommand("  Cash In Hand  ", requiresAttachment);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(generatedId);

        savedMethod.Should().NotBeNull();
        savedMethod!.Name.Should().Be("Cash In Hand");
        savedMethod.RequiresAttachment.Should().Be(requiresAttachment);
        savedMethod.IsActive.Should().BeTrue();

        await _repository.Received(1).AddAsync(Arg.Any<WithdrawMethod>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _repository.GetByNameAsync("Cheque", ct).Returns((WithdrawMethod?)null);

        var command = new CreateWithdrawMethodCommand("Cheque", RequiresAttachment: true);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByNameAsync("Cheque", ct);
        await _repository.Received(1).AddAsync(Arg.Any<WithdrawMethod>(), ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
