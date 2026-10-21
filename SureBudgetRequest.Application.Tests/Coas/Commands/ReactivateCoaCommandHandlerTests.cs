using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Coas.Commands.ReactivateCoa;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Coas.Commands;

public class ReactivateCoaCommandHandlerTests
{
    private readonly ICoaRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ReactivateCoaCommandHandler _handler;

    public ReactivateCoaCommandHandlerTests()
    {
        _repository = Substitute.For<ICoaRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new ReactivateCoaCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_CoaExists_Inactive_ShouldReactivateAndReturnSuccess()
    {
        // Arrange
        var coaId = Guid.NewGuid();
        var coa = new Coa("5010", "Subscriptions");
        coa.Deactivate();
        coa.IsActive.Should().BeFalse();

        _repository.GetByIdAsync(coaId, Arg.Any<CancellationToken>())
            .Returns(coa);

        var command = new ReactivateCoaCommand(coaId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        coa.IsActive.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CoaAlreadyActive_ShouldRemainActiveAndReturnSuccess()
    {
        // Arrange
        var coaId = Guid.NewGuid();
        var coa = new Coa("5010", "Subscriptions");
        coa.IsActive.Should().BeTrue();

        _repository.GetByIdAsync(coaId, Arg.Any<CancellationToken>())
            .Returns(coa);

        var command = new ReactivateCoaCommand(coaId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        coa.IsActive.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CoaNotFound_ShouldReturnNotFoundFailure()
    {
        // Arrange
        var coaId = Guid.NewGuid();
        _repository.GetByIdAsync(coaId, Arg.Any<CancellationToken>())
            .Returns((Coa?)null);

        var command = new ReactivateCoaCommand(coaId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CoaErrors.NotFound.Code);
        result.Error.Message.Should().Be(CoaErrors.NotFound.Message);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancellationToken_ShouldBePropagatedToDependencies()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var coaId = Guid.NewGuid();
        var coa = new Coa("5010", "Subscriptions");

        _repository.GetByIdAsync(coaId, ct).Returns(coa);

        var command = new ReactivateCoaCommand(coaId);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByIdAsync(coaId, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
