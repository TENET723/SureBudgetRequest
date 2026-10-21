using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.WithdrawMethods.Commands.UpdateWithdrawMethod;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.WithdrawMethods.Commands;

public class UpdateWithdrawMethodCommandHandlerTests
{
    private readonly IWithdrawMethodRepository _repository = Substitute.For<IWithdrawMethodRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly UpdateWithdrawMethodCommandHandler _handler;

    public UpdateWithdrawMethodCommandHandlerTests()
    {
        _handler = new UpdateWithdrawMethodCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenMethodNotFound_ReturnsGenericNotFoundError()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((WithdrawMethod?)null);

        var command = new UpdateWithdrawMethodCommand(id, "Cash", IsActive: true, RequiresAttachment: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WithdrawMethodErrors.GenericNotFound);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRenamingToNameTakenByDifferentMethod_ReturnsAlreadyExistsError()
    {
        // Arrange
        var id = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var currentMethod = new WithdrawMethod("Old Name");
        typeof(WithdrawMethod).GetProperty(nameof(WithdrawMethod.Id))!.SetValue(currentMethod, id);

        var existingOther = new WithdrawMethod("New Name");
        typeof(WithdrawMethod).GetProperty(nameof(WithdrawMethod.Id))!.SetValue(existingOther, otherId);

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(currentMethod);
        _repository.GetByNameAsync("New Name", Arg.Any<CancellationToken>()).Returns(existingOther);

        var command = new UpdateWithdrawMethodCommand(id, "New Name", IsActive: true, RequiresAttachment: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WithdrawMethodErrors.AlreadyExists("New Name"));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNameUnchanged_DoesNotQueryGetByName()
    {
        // Arrange
        var id = Guid.NewGuid();
        var method = new WithdrawMethod("Bank Transfer", requiresAttachment: false);
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(method);

        var command = new UpdateWithdrawMethodCommand(id, "  bank transfer  ", IsActive: true, RequiresAttachment: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        method.RequiresAttachment.Should().BeTrue();

        await _repository.DidNotReceive().GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExistingHasSameId_AllowsUpdate()
    {
        // Arrange
        var id = Guid.NewGuid();
        var method = new WithdrawMethod("Old Name");
        typeof(WithdrawMethod).GetProperty(nameof(WithdrawMethod.Id))!.SetValue(method, id);

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(method);
        _repository.GetByNameAsync("New Name", Arg.Any<CancellationToken>()).Returns(method);

        var command = new UpdateWithdrawMethodCommand(id, "New Name", IsActive: true, RequiresAttachment: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        method.Name.Should().Be("New Name");
        method.RequiresAttachment.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenNameIsNullOrWhiteSpace_ReturnsValidationError(string? invalidName)
    {
        // Arrange
        var id = Guid.NewGuid();
        var method = new WithdrawMethod("Current Name");
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(method);
        _repository.GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((WithdrawMethod?)null);

        var command = new UpdateWithdrawMethodCommand(id, invalidName!, IsActive: true, RequiresAttachment: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WithdrawMethodErrors.ValidationError("").Code);
        result.Error.Message.Should().Contain("Name is required");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUpdatingToActive_ReactivatesAndUpdatesProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var method = new WithdrawMethod("Old Name", requiresAttachment: false);
        method.Deactivate();
        method.IsActive.Should().BeFalse();

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(method);
        _repository.GetByNameAsync("New Name", Arg.Any<CancellationToken>()).Returns((WithdrawMethod?)null);

        var command = new UpdateWithdrawMethodCommand(id, "New Name", IsActive: true, RequiresAttachment: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        method.Name.Should().Be("New Name");
        method.RequiresAttachment.Should().BeTrue();
        method.IsActive.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUpdatingToInactive_DeactivatesAndUpdatesProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var method = new WithdrawMethod("Old Name", requiresAttachment: true);
        method.IsActive.Should().BeTrue();

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(method);
        _repository.GetByNameAsync("New Name", Arg.Any<CancellationToken>()).Returns((WithdrawMethod?)null);

        var command = new UpdateWithdrawMethodCommand(id, "New Name", IsActive: false, RequiresAttachment: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        method.Name.Should().Be("New Name");
        method.RequiresAttachment.Should().BeFalse();
        method.IsActive.Should().BeFalse();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var id = Guid.NewGuid();
        var method = new WithdrawMethod("Old Name");
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _repository.GetByIdAsync(id, ct).Returns(method);
        _repository.GetByNameAsync("New Name", ct).Returns((WithdrawMethod?)null);

        var command = new UpdateWithdrawMethodCommand(id, "New Name", IsActive: true, RequiresAttachment: false);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByIdAsync(id, ct);
        await _repository.Received(1).GetByNameAsync("New Name", ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
