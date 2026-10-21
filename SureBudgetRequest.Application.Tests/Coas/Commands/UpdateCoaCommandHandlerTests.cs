using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Coas.Commands.UpdateCoa;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Coas.Commands;

public class UpdateCoaCommandHandlerTests
{
    private readonly ICoaRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateCoaCommandHandler _handler;

    public UpdateCoaCommandHandlerTests()
    {
        _repository = Substitute.For<ICoaRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new UpdateCoaCommandHandler(_repository, _unitOfWork);
    }

    private static Coa CreateCoa(Guid id, string code, string name, string? description = null, bool isActive = true)
    {
        var coa = new Coa(code, name, description);
        typeof(Coa).GetProperty(nameof(Coa.Id))!.SetValue(coa, id);
        if (!isActive) coa.Deactivate();
        return coa;
    }

    [Fact]
    public async Task Handle_CoaExists_ValidChangeAndReactivate_ShouldUpdateAndSave()
    {
        // Arrange
        var coaId = Guid.NewGuid();
        var coa = CreateCoa(coaId, "5010", "Old Name", "Old Desc", isActive: false);

        _repository.GetByIdAsync(coaId, Arg.Any<CancellationToken>())
            .Returns(coa);
        _repository.GetByCodeAsync("5020", Arg.Any<CancellationToken>())
            .Returns((Coa?)null);

        var command = new UpdateCoaCommand(coaId, "5020", "New Name", "New Desc", IsActive: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        coa.Code.Should().Be("5020");
        coa.Name.Should().Be("New Name");
        coa.Description.Should().Be("New Desc");
        coa.IsActive.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CoaExists_DeactivateRequested_ShouldDeactivateAndSave()
    {
        // Arrange
        var coaId = Guid.NewGuid();
        var coa = CreateCoa(coaId, "5010", "Consulting", isActive: true);

        _repository.GetByIdAsync(coaId, Arg.Any<CancellationToken>())
            .Returns(coa);

        var command = new UpdateCoaCommand(coaId, "5010", "Consulting", null, IsActive: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        coa.IsActive.Should().BeFalse();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SameCodeDifferentWhitespace_ShouldNotQueryDuplicateCheck()
    {
        // Arrange
        var coaId = Guid.NewGuid();
        var coa = CreateCoa(coaId, "5010", "Training");

        _repository.GetByIdAsync(coaId, Arg.Any<CancellationToken>())
            .Returns(coa);

        var command = new UpdateCoaCommand(coaId, "  5010  ", "Training Updated", null, IsActive: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        coa.Name.Should().Be("Training Updated");

        await _repository.DidNotReceive().GetByCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateCodeOnAnotherCoa_ShouldReturnConflictFailure()
    {
        // Arrange
        var coaIdA = Guid.NewGuid();
        var coaA = CreateCoa(coaIdA, "5010", "Name A");

        var coaIdB = Guid.NewGuid();
        var coaB = CreateCoa(coaIdB, "5020", "Name B");

        _repository.GetByIdAsync(coaIdA, Arg.Any<CancellationToken>())
            .Returns(coaA);
        _repository.GetByCodeAsync("5020", Arg.Any<CancellationToken>())
            .Returns(coaB);

        var command = new UpdateCoaCommand(coaIdA, "5020", "Updated Name", null, IsActive: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CoaErrors.AlreadyExists("5020").Code);
        result.Error.Message.Should().Be(CoaErrors.AlreadyExists("5020").Message);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CoaNotFound_ShouldReturnNotFoundFailure()
    {
        // Arrange
        var coaId = Guid.NewGuid();
        _repository.GetByIdAsync(coaId, Arg.Any<CancellationToken>())
            .Returns((Coa?)null);

        var command = new UpdateCoaCommand(coaId, "5010", "Any Name", null, IsActive: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CoaErrors.NotFound.Code);
        result.Error.Message.Should().Be(CoaErrors.NotFound.Message);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceCode_ShouldCatchDomainExceptionAndReturnValidationError(string? invalidCode)
    {
        // Arrange
        var coaId = Guid.NewGuid();
        var coa = CreateCoa(coaId, "5010", "Valid Name");

        _repository.GetByIdAsync(coaId, Arg.Any<CancellationToken>())
            .Returns(coa);
        _repository.GetByCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Coa?)null);

        var command = new UpdateCoaCommand(coaId, invalidCode!, "Valid Name", null, IsActive: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Coa.Validation");
        result.Error.Message.Should().Contain("Code is required.");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceName_ShouldCatchDomainExceptionAndReturnValidationError(string? invalidName)
    {
        // Arrange
        var coaId = Guid.NewGuid();
        var coa = CreateCoa(coaId, "5010", "Valid Name");

        _repository.GetByIdAsync(coaId, Arg.Any<CancellationToken>())
            .Returns(coa);

        var command = new UpdateCoaCommand(coaId, "5010", invalidName!, null, IsActive: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Coa.Validation");
        result.Error.Message.Should().Contain("Name is required.");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancellationToken_ShouldBePropagatedToDependencies()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var coaId = Guid.NewGuid();
        var coa = CreateCoa(coaId, "5010", "Old Name");

        _repository.GetByIdAsync(coaId, ct).Returns(coa);
        _repository.GetByCodeAsync("5020", ct).Returns((Coa?)null);

        var command = new UpdateCoaCommand(coaId, "5020", "New Name", null, true);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByIdAsync(coaId, ct);
        await _repository.Received(1).GetByCodeAsync("5020", ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
