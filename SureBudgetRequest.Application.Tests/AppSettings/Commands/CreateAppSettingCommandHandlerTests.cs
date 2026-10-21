using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.AppSettings.Commands.CreateAppSetting;
using SureBudgetRequest.Domain.Entities;
using Xunit;

namespace SureBudgetRequest.Application.Tests.AppSettings.Commands;

public class CreateAppSettingCommandHandlerTests
{
    private readonly IAppSettingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreateAppSettingCommandHandler _handler;

    public CreateAppSettingCommandHandlerTests()
    {
        _repository = Substitute.For<IAppSettingRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new CreateAppSettingCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldAddSettingAndReturnSuccess()
    {
        // Arrange
        var command = new CreateAppSettingCommand("MaxAdvanceLimit", "5000000", "Maximum advance per request");
        _repository.GetByKeyAsync(command.Key, Arg.Any<CancellationToken>())
            .Returns((AppSetting?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _repository.Received(1).AddAsync(
            Arg.Is<AppSetting>(s =>
                s.Key == "MaxAdvanceLimit" &&
                s.Value == "5000000" &&
                s.Description == "Maximum advance per request"),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceKey_ShouldReturnFailure(string? invalidKey)
    {
        // Arrange
        var command = new CreateAppSettingCommand(invalidKey!, "123", "Some desc");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Key is required.");

        await _repository.DidNotReceive().GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AddAsync(Arg.Any<AppSetting>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateKey_ShouldReturnFailure()
    {
        // Arrange
        var command = new CreateAppSettingCommand("ExistingKey", "NewVal", "NewDesc");
        var existingSetting = new AppSetting("ExistingKey", "OldVal", "OldDesc");

        _repository.GetByKeyAsync(command.Key, Arg.Any<CancellationToken>())
            .Returns(existingSetting);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("A setting with key 'ExistingKey' already exists.");

        await _repository.DidNotReceive().AddAsync(Arg.Any<AppSetting>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancellationToken_ShouldBePropagatedToDependencies()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var command = new CreateAppSettingCommand("UniqueKey", "Val", null);

        _repository.GetByKeyAsync(command.Key, ct).Returns((AppSetting?)null);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByKeyAsync(command.Key, ct);
        await _repository.Received(1).AddAsync(Arg.Any<AppSetting>(), ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
