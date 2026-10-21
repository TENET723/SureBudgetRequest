using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Coas.Commands.CreateCoa;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Coas.Commands;

public class CreateCoaCommandHandlerTests
{
    private readonly ICoaRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreateCoaCommandHandler _handler;

    public CreateCoaCommandHandlerTests()
    {
        _repository = Substitute.For<ICoaRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new CreateCoaCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldAddCoaAndReturnSuccessWithId()
    {
        // Arrange
        var command = new CreateCoaCommand("5010", "Software Subscriptions", "SaaS tooling");
        _repository.GetByCodeAsync(command.Code, Arg.Any<CancellationToken>())
            .Returns((Coa?)null);

        var generatedId = Guid.NewGuid();
        _repository.AddAsync(Arg.Any<Coa>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var coa = callInfo.Arg<Coa>();
                typeof(Coa).GetProperty(nameof(Coa.Id))!.SetValue(coa, generatedId);
                return Task.CompletedTask;
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(generatedId);

        await _repository.Received(1).AddAsync(
            Arg.Is<Coa>(c =>
                c.Code == "5010" &&
                c.Name == "Software Subscriptions" &&
                c.Description == "SaaS tooling"),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateCode_ShouldReturnConflictFailure()
    {
        // Arrange
        var command = new CreateCoaCommand("5010", "Duplicate Code Name");
        var existingCoa = new Coa("5010", "Original Name");

        _repository.GetByCodeAsync(command.Code, Arg.Any<CancellationToken>())
            .Returns(existingCoa);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CoaErrors.AlreadyExists("5010").Code);
        result.Error.Message.Should().Be(CoaErrors.AlreadyExists("5010").Message);

        await _repository.DidNotReceive().AddAsync(Arg.Any<Coa>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceCode_ShouldCatchDomainExceptionAndReturnValidationError(string? invalidCode)
    {
        // Arrange
        var command = new CreateCoaCommand(invalidCode!, "Valid Name");
        _repository.GetByCodeAsync(command.Code, Arg.Any<CancellationToken>())
            .Returns((Coa?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Coa.Validation");
        result.Error.Message.Should().Contain("Code is required.");

        await _repository.DidNotReceive().AddAsync(Arg.Any<Coa>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceName_ShouldCatchDomainExceptionAndReturnValidationError(string? invalidName)
    {
        // Arrange
        var command = new CreateCoaCommand("5020", invalidName!);
        _repository.GetByCodeAsync(command.Code, Arg.Any<CancellationToken>())
            .Returns((Coa?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Coa.Validation");
        result.Error.Message.Should().Contain("Name is required.");

        await _repository.DidNotReceive().AddAsync(Arg.Any<Coa>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancellationToken_ShouldBePropagatedToDependencies()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var command = new CreateCoaCommand("5030", "Consulting");

        _repository.GetByCodeAsync(command.Code, ct).Returns((Coa?)null);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByCodeAsync(command.Code, ct);
        await _repository.Received(1).AddAsync(Arg.Any<Coa>(), ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
