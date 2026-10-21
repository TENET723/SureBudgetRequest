using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetCategories.Commands.ReactivateBudgetCategory;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetCategories.Commands;

public class ReactivateBudgetCategoryCommandHandlerTests
{
    private readonly IBudgetCategoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ReactivateBudgetCategoryCommandHandler _handler;

    public ReactivateBudgetCategoryCommandHandlerTests()
    {
        _repository = Substitute.For<IBudgetCategoryRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new ReactivateBudgetCategoryCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_CategoryExists_Inactive_ShouldReactivateAndReturnSuccess()
    {
        // Arrange
        var catId = Guid.NewGuid();
        var category = new BudgetCategory("Client Entertainment");
        category.Deactivate();
        category.IsActive.Should().BeFalse();

        _repository.GetByIdAsync(catId, Arg.Any<CancellationToken>())
            .Returns(category);

        var command = new ReactivateBudgetCategoryCommand(catId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        category.IsActive.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CategoryAlreadyActive_ShouldRemainActiveAndReturnSuccess()
    {
        // Arrange
        var catId = Guid.NewGuid();
        var category = new BudgetCategory("Office Supplies");
        category.IsActive.Should().BeTrue();

        _repository.GetByIdAsync(catId, Arg.Any<CancellationToken>())
            .Returns(category);

        var command = new ReactivateBudgetCategoryCommand(catId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        category.IsActive.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CategoryNotFound_ShouldReturnGenericNotFoundFailure()
    {
        // Arrange
        var catId = Guid.NewGuid();
        _repository.GetByIdAsync(catId, Arg.Any<CancellationToken>())
            .Returns((BudgetCategory?)null);

        var command = new ReactivateBudgetCategoryCommand(catId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BudgetCategoryErrors.GenericNotFound.Code);
        result.Error.Message.Should().Be(BudgetCategoryErrors.GenericNotFound.Message);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancellationToken_ShouldBePropagatedToDependencies()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var catId = Guid.NewGuid();
        var category = new BudgetCategory("Marketing");

        _repository.GetByIdAsync(catId, ct).Returns(category);

        var command = new ReactivateBudgetCategoryCommand(catId);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByIdAsync(catId, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
