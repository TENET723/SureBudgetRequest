using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Currencies.Commands.CreateCurrency;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Currencies.Commands;

public class CreateCurrencyCommandHandlerTests
{
    private readonly ICurrencyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreateCurrencyCommandHandler _handler;

    public CreateCurrencyCommandHandlerTests()
    {
        _repository = Substitute.For<ICurrencyRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new CreateCurrencyCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldAddCurrencyAndReturnSuccessWithCode()
    {
        // Arrange
        var command = new CreateCurrencyCommand("USD", "US Dollar", 4200m);
        _repository.GetByCodeAsync(command.Code, Arg.Any<CancellationToken>())
            .Returns((Currency?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("USD");

        await _repository.Received(1).AddAsync(
            Arg.Is<Currency>(c =>
                c.Code == "USD" &&
                c.Name == "US Dollar" &&
                c.RateToMmk == 4200m),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateCode_ShouldReturnConflictFailure()
    {
        // Arrange
        var command = new CreateCurrencyCommand("EUR", "Euro", 4500m);
        var existingCurrency = new Currency("EUR", "Euro", 4500m);

        _repository.GetByCodeAsync(command.Code, Arg.Any<CancellationToken>())
            .Returns(existingCurrency);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CurrencyErrors.AlreadyExists("EUR").Code);
        result.Error.Message.Should().Be(CurrencyErrors.AlreadyExists("EUR").Message);

        await _repository.DidNotReceive().AddAsync(Arg.Any<Currency>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceCode_ShouldCatchDomainExceptionAndReturnValidationError(string? invalidCode)
    {
        // Arrange
        var command = new CreateCurrencyCommand(invalidCode!, "Valid Name", 100m);
        _repository.GetByCodeAsync(command.Code, Arg.Any<CancellationToken>())
            .Returns((Currency?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.Validation");
        result.Error.Message.Should().Contain("Currency code is required.");

        await _repository.DidNotReceive().AddAsync(Arg.Any<Currency>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceName_ShouldCatchDomainExceptionAndReturnValidationError(string? invalidName)
    {
        // Arrange
        var command = new CreateCurrencyCommand("SGD", invalidName!, 3100m);
        _repository.GetByCodeAsync(command.Code, Arg.Any<CancellationToken>())
            .Returns((Currency?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.Validation");
        result.Error.Message.Should().Contain("Currency name is required.");

        await _repository.DidNotReceive().AddAsync(Arg.Any<Currency>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-1000)]
    public async Task Handle_ZeroOrNegativeRate_ShouldCatchDomainExceptionAndReturnValidationError(decimal invalidRate)
    {
        // Arrange
        var command = new CreateCurrencyCommand("THB", "Thai Baht", invalidRate);
        _repository.GetByCodeAsync(command.Code, Arg.Any<CancellationToken>())
            .Returns((Currency?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.Validation");
        result.Error.Message.Should().Contain("Rate must be greater than zero.");

        await _repository.DidNotReceive().AddAsync(Arg.Any<Currency>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancellationToken_ShouldBePropagatedToDependencies()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var command = new CreateCurrencyCommand("JPY", "Japanese Yen", 28m);

        _repository.GetByCodeAsync(command.Code, ct).Returns((Currency?)null);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByCodeAsync(command.Code, ct);
        await _repository.Received(1).AddAsync(Arg.Any<Currency>(), ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
