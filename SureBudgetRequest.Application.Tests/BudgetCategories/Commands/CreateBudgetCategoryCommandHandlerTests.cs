using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetCategories.Commands.CreateBudgetCategory;
using SureBudgetRequest.Domain.Entities;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetCategories.Commands;

public class CreateBudgetCategoryCommandHandlerTests
{
    private readonly IBudgetCategoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreateBudgetCategoryCommandHandler _handler;

    public CreateBudgetCategoryCommandHandlerTests()
    {
        _repository = Substitute.For<IBudgetCategoryRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new CreateBudgetCategoryCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldAddCategoryAndReturnSuccessWithId()
    {
        // Arrange
        var command = new CreateBudgetCategoryCommand("Marketing & Outreach");
        _repository.GetByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns((BudgetCategory?)null);

        var generatedId = Guid.NewGuid();
        _repository.AddAsync(Arg.Any<BudgetCategory>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cat = callInfo.Arg<BudgetCategory>();
                typeof(BudgetCategory).GetProperty(nameof(BudgetCategory.Id))!.SetValue(cat, generatedId);
                return Task.CompletedTask;
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(generatedId);

        await _repository.Received(1).AddAsync(
            Arg.Is<BudgetCategory>(c => c.Name == "Marketing & Outreach"),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateName_ShouldReturnFailure()
    {
        // Arrange
        var command = new CreateBudgetCategoryCommand("Engineering");
        var existingCategory = new BudgetCategory("Engineering");

        _repository.GetByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(existingCategory);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Budget category 'Engineering' already exists.");

        await _repository.DidNotReceive().AddAsync(Arg.Any<BudgetCategory>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceName_ShouldCatchDomainExceptionAndReturnFailure(string? invalidName)
    {
        // Arrange
        var command = new CreateBudgetCategoryCommand(invalidName!);
        _repository.GetByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns((BudgetCategory?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Name is required.");

        await _repository.DidNotReceive().AddAsync(Arg.Any<BudgetCategory>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancellationToken_ShouldBePropagatedToDependencies()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var command = new CreateBudgetCategoryCommand("Operations");

        _repository.GetByNameAsync(command.Name, ct).Returns((BudgetCategory?)null);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByNameAsync(command.Name, ct);
        await _repository.Received(1).AddAsync(Arg.Any<BudgetCategory>(), ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
