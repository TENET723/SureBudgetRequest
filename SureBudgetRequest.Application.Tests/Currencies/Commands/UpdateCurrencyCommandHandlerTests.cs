using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Currencies.Commands.UpdateCurrencyRate;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Currencies.Commands;

public class UpdateCurrencyCommandHandlerTests
{
    private readonly ICurrencyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateCurrencyCommandHandler _handler;

    public UpdateCurrencyCommandHandlerTests()
    {
        _repository = Substitute.For<ICurrencyRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new UpdateCurrencyCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidRateAndNameChange_ShouldUpdateAndRecordRateChangeAudit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currency = new Currency("USD", "US Dollar", 4000m);

        _repository.GetByCodeAsync("USD", Arg.Any<CancellationToken>())
            .Returns(currency);

        var command = new UpdateCurrencyCommand("USD", "United States Dollar", 4200m, IsActive: true, ChangedByUserId: userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        currency.Name.Should().Be("United States Dollar");
        currency.RateToMmk.Should().Be(4200m);

        await _repository.Received(1).AddRateChangeAsync(
            Arg.Is<CurrencyRateChange>(a =>
                a.CurrencyCode == "USD" &&
                a.OldRate == 4000m &&
                a.NewRate == 4200m &&
                a.ChangedByUserId == userId),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SameRate_ShouldNotRecordRateChangeAudit()
    {
        // Arrange
        var currency = new Currency("USD", "US Dollar", 4000m);

        _repository.GetByCodeAsync("USD", Arg.Any<CancellationToken>())
            .Returns(currency);

        var command = new UpdateCurrencyCommand("USD", "Renamed USD", 4000m, IsActive: true, ChangedByUserId: Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        currency.Name.Should().Be("Renamed USD");

        await _repository.DidNotReceive().AddRateChangeAsync(
            Arg.Any<CurrencyRateChange>(), Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CurrencyNotFound_ShouldReturnNotFoundFailure()
    {
        // Arrange
        var command = new UpdateCurrencyCommand("GBP", "British Pound", 5000m, true, Guid.NewGuid());
        _repository.GetByCodeAsync("GBP", Arg.Any<CancellationToken>())
            .Returns((Currency?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CurrencyErrors.NotFound("GBP").Code);
        result.Error.Message.Should().Be(CurrencyErrors.NotFound("GBP").Message);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceName_ShouldCatchDomainExceptionAndReturnValidationError(string? invalidName)
    {
        // Arrange
        var currency = new Currency("USD", "US Dollar", 4000m);
        _repository.GetByCodeAsync("USD", Arg.Any<CancellationToken>())
            .Returns(currency);

        var command = new UpdateCurrencyCommand("USD", invalidName!, 4100m, true, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.Validation");
        result.Error.Message.Should().Contain("Currency name is required.");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-500)]
    public async Task Handle_InvalidRate_ZeroOrNegative_ShouldCatchDomainExceptionAndReturnValidationError(decimal invalidRate)
    {
        // Arrange
        var currency = new Currency("USD", "US Dollar", 4000m);
        _repository.GetByCodeAsync("USD", Arg.Any<CancellationToken>())
            .Returns(currency);

        var command = new UpdateCurrencyCommand("USD", "US Dollar", invalidRate, true, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.Validation");
        result.Error.Message.Should().Contain("Rate must be greater than zero.");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BaseCurrencyRateChange_ShouldCatchDomainExceptionAndReturnValidationError()
    {
        // Arrange: MMK is permanently fixed at 1.0
        var mmk = new Currency("MMK", "Myanmar Kyat", 1m);
        _repository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>())
            .Returns(mmk);

        var command = new UpdateCurrencyCommand("MMK", "Myanmar Kyat", 1.5m, true, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.Validation");
        result.Error.Message.Should().Contain("fixed at 1");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BaseCurrencyDeactivation_ShouldCatchDomainExceptionAndReturnValidationError()
    {
        // Arrange: MMK cannot be deactivated
        var mmk = new Currency("MMK", "Myanmar Kyat", 1m);
        _repository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>())
            .Returns(mmk);

        var command = new UpdateCurrencyCommand("MMK", "Myanmar Kyat", 1m, IsActive: false, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.Validation");
        result.Error.Message.Should().Contain("cannot be deactivated");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ToggleIsActive_NonBaseCurrency_ShouldSucceed()
    {
        // Arrange
        var currency = new Currency("THB", "Thai Baht", 100m);
        _repository.GetByCodeAsync("THB", Arg.Any<CancellationToken>())
            .Returns(currency);

        var command = new UpdateCurrencyCommand("THB", "Thai Baht", 100m, IsActive: false, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        currency.IsActive.Should().BeFalse();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancellationToken_ShouldBePropagatedToDependencies()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var currency = new Currency("SGD", "Singapore Dollar", 3000m);

        _repository.GetByCodeAsync("SGD", ct).Returns(currency);

        var command = new UpdateCurrencyCommand("SGD", "Singapore Dollar", 3100m, true, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByCodeAsync("SGD", ct);
        await _repository.Received(1).AddRateChangeAsync(Arg.Any<CurrencyRateChange>(), ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
