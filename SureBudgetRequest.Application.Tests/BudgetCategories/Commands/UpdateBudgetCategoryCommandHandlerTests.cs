using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetCategories.Commands.UpdateBudgetCategory;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetCategories.Commands;

public class UpdateBudgetCategoryCommandHandlerTests
{
    private readonly IBudgetCategoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateBudgetCategoryCommandHandler _handler;

    public UpdateBudgetCategoryCommandHandlerTests()
    {
        _repository = Substitute.For<IBudgetCategoryRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new UpdateBudgetCategoryCommandHandler(_repository, _unitOfWork);
    }

    private static BudgetCategory CreateCategory(Guid id, string name, bool isActive = true)
    {
        var category = new BudgetCategory(name);
        typeof(BudgetCategory).GetProperty(nameof(BudgetCategory.Id))!.SetValue(category, id);
        if (!isActive) category.Deactivate();
        return category;
    }

    [Fact]
    public async Task Handle_CategoryExists_NewUniqueName_ShouldRenameReactivateAndSave()
    {
        // Arrange
        var catId = Guid.NewGuid();
        var category = CreateCategory(catId, "Old Name", isActive: false);

        _repository.GetByIdAsync(catId, Arg.Any<CancellationToken>())
            .Returns(category);
        _repository.GetByNameAsync("Brand Marketing", Arg.Any<CancellationToken>())
            .Returns((BudgetCategory?)null);

        var command = new UpdateBudgetCategoryCommand(catId, "Brand Marketing", IsActive: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        category.Name.Should().Be("Brand Marketing");
        category.IsActive.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CategoryExists_DeactivateRequested_ShouldDeactivateAndSave()
    {
        // Arrange
        var catId = Guid.NewGuid();
        var category = CreateCategory(catId, "Operations", isActive: true);

        _repository.GetByIdAsync(catId, Arg.Any<CancellationToken>())
            .Returns(category);

        var command = new UpdateBudgetCategoryCommand(catId, "Operations", IsActive: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        category.IsActive.Should().BeFalse();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SameNameDifferentCaseOrWhitespace_ShouldNotQueryDuplicateOrConflict()
    {
        // Arrange
        var catId = Guid.NewGuid();
        var category = CreateCategory(catId, "Finance", isActive: true);

        _repository.GetByIdAsync(catId, Arg.Any<CancellationToken>())
            .Returns(category);

        var command = new UpdateBudgetCategoryCommand(catId, "  finance  ", IsActive: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        category.Name.Should().Be("finance");

        await _repository.DidNotReceive().GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateNameOnDifferentCategory_ShouldReturnConflictFailure()
    {
        // Arrange
        var catIdA = Guid.NewGuid();
        var categoryA = CreateCategory(catIdA, "Marketing");

        var catIdB = Guid.NewGuid();
        var categoryB = CreateCategory(catIdB, "Human Resources");

        _repository.GetByIdAsync(catIdA, Arg.Any<CancellationToken>())
            .Returns(categoryA);
        _repository.GetByNameAsync("Human Resources", Arg.Any<CancellationToken>())
            .Returns(categoryB);

        var command = new UpdateBudgetCategoryCommand(catIdA, "Human Resources", IsActive: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BudgetCategoryErrors.AlreadyExists("Human Resources").Code);
        result.Error.Message.Should().Be(BudgetCategoryErrors.AlreadyExists("Human Resources").Message);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CategoryNotFound_ShouldReturnGenericNotFoundFailure()
    {
        // Arrange
        var catId = Guid.NewGuid();
        _repository.GetByIdAsync(catId, Arg.Any<CancellationToken>())
            .Returns((BudgetCategory?)null);

        var command = new UpdateBudgetCategoryCommand(catId, "Any Name", IsActive: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BudgetCategoryErrors.GenericNotFound.Code);
        result.Error.Message.Should().Be(BudgetCategoryErrors.GenericNotFound.Message);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceName_ShouldCatchDomainExceptionAndReturnValidationError(string? invalidName)
    {
        // Arrange
        var catId = Guid.NewGuid();
        var category = CreateCategory(catId, "Valid Category");

        _repository.GetByIdAsync(catId, Arg.Any<CancellationToken>())
            .Returns(category);

        var command = new UpdateBudgetCategoryCommand(catId, invalidName!, IsActive: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BudgetCategory.Validation");
        result.Error.Message.Should().Contain("Name is required.");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancellationToken_ShouldBePropagatedToDependencies()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var catId = Guid.NewGuid();
        var category = CreateCategory(catId, "Logistics");

        _repository.GetByIdAsync(catId, ct).Returns(category);
        _repository.GetByNameAsync("Supply Chain", ct).Returns((BudgetCategory?)null);

        var command = new UpdateBudgetCategoryCommand(catId, "Supply Chain", true);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByIdAsync(catId, ct);
        await _repository.Received(1).GetByNameAsync("Supply Chain", ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
