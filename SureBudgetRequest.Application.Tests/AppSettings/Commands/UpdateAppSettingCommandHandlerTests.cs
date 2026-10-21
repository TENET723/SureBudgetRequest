using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.AppSettings.Commands.UpdateAppSetting;
using SureBudgetRequest.Domain.Entities;
using Xunit;

namespace SureBudgetRequest.Application.Tests.AppSettings.Commands;

public class UpdateAppSettingCommandHandlerTests
{
    private readonly IAppSettingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateAppSettingCommandHandler _handler;

    public UpdateAppSettingCommandHandlerTests()
    {
        _repository = Substitute.For<IAppSettingRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new UpdateAppSettingCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_SettingExists_ShouldUpdatePropertiesAndReturnSuccess()
    {
        // Arrange
        var setting = new AppSetting("ThemeColor", "Blue", "User theme color");
        var command = new UpdateAppSettingCommand("ThemeColor", "DarkGreen", "Updated theme color");

        _repository.GetByKeyAsync(command.Key, Arg.Any<CancellationToken>())
            .Returns(setting);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        setting.Value.Should().Be("DarkGreen");
        setting.Description.Should().Be("Updated theme color");

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SettingNotFound_ShouldReturnFailure()
    {
        // Arrange
        var command = new UpdateAppSettingCommand("NonExistentKey", "Value", "Description");
        _repository.GetByKeyAsync(command.Key, Arg.Any<CancellationToken>())
            .Returns((AppSetting?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("App setting 'NonExistentKey' not found.");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_UpdateDescriptionWithNullOrWhitespace_ShouldUpdateDescription(string? newDescription)
    {
        // Arrange
        var setting = new AppSetting("ThemeColor", "Blue", "Original description");
        var command = new UpdateAppSettingCommand("ThemeColor", "Red", newDescription);

        _repository.GetByKeyAsync(command.Key, Arg.Any<CancellationToken>())
            .Returns(setting);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        setting.Value.Should().Be("Red");
        setting.Description.Should().Be(newDescription);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancellationToken_ShouldBePropagatedToDependencies()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var setting = new AppSetting("Key1", "Val1");
        var command = new UpdateAppSettingCommand("Key1", "Val2", null);

        _repository.GetByKeyAsync(command.Key, ct).Returns(setting);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByKeyAsync(command.Key, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
