using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.AssignAccountingCategory;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class AssignAccountingCategoryCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IAccountingCategoryRepository _accountingCategoryRepository = Substitute.For<IAccountingCategoryRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AssignAccountingCategoryCommandHandler _handler;

    private readonly Guid _actorId = Guid.NewGuid();

    public AssignAccountingCategoryCommandHandlerTests()
    {
        _currentUser.UserId.Returns(_actorId);
        _handler = new AssignAccountingCategoryCommandHandler(
            _budgetRequestRepository,
            _accountingCategoryRepository,
            _currentUser,
            _unitOfWork);
    }

    private static BudgetRequest CreateApprovedRequest()
    {
        var headId = Guid.NewGuid();
        var request = BudgetRequest.CreateDraft(
            Guid.NewGuid(), "Alice", BudgetRequestType.Standard, headId, "Bob Head",
            DateTime.UtcNow.AddDays(2), 200_000m, "MMK", "Supplies", "Alice", "Job",
            Guid.NewGuid(), false, null);

        request.Submit(
            Guid.NewGuid(), 1_000_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, headId, "Bob Head", "Alice", false, 1);

        request.ApproveBy(headId);
        request.ApproveBy(Guid.NewGuid(), coaId: Guid.NewGuid());
        return request;
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Finance)]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Management)]
    [InlineData(UserRole.Support)]
    public async Task Handle_WhenRoleIsNotAccounting_ReturnsOnlyAccountingCanCategorizeError(UserRole role)
    {
        // Arrange
        _currentUser.Role.Returns(role);
        var command = new AssignAccountingCategoryCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyAccountingCanCategorize);
    }

    [Fact]
    public async Task Handle_WhenRoleIsAccounting_AssignsCategoryAndSaves()
    {
        // Arrange
        _currentUser.Role.Returns(UserRole.Accounting);
        var request = CreateApprovedRequest();
        var categoryId = Guid.NewGuid();
        var category = new AccountingCategory("Marketing");
        typeof(AccountingCategory).GetProperty("Id")!.SetValue(category, categoryId);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>())
            .Returns(request);
        _accountingCategoryRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns(category);

        var command = new AssignAccountingCategoryCommand(request.Id, categoryId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.AccountingCategoryId.Should().Be(categoryId);
        request.ApprovalActions.Should().Contain(a =>
            a.Stage == ApprovalStage.Accounting &&
            a.Decision == ApprovalDecision.Categorized &&
            a.ApproverId == _currentUser.UserId &&
            a.Comment == "Accounting Category: Marketing");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenBudgetRequestNotFound_ReturnsNotFound()
    {
        // Arrange
        _currentUser.Role.Returns(UserRole.Accounting);
        var requestId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        _budgetRequestRepository.GetByIdAsync(requestId, Arg.Any<CancellationToken>())
            .Returns((BudgetRequest?)null);

        var command = new AssignAccountingCategoryCommand(requestId, categoryId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BudgetRequestErrors.NotFound(requestId).Code);
    }

    [Fact]
    public async Task Handle_WhenAccountingCategoryNotFound_ReturnsGenericNotFound()
    {
        // Arrange
        _currentUser.Role.Returns(UserRole.Accounting);
        var request = CreateApprovedRequest();
        var categoryId = Guid.NewGuid();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>())
            .Returns(request);
        _accountingCategoryRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns((AccountingCategory?)null);

        var command = new AssignAccountingCategoryCommand(request.Id, categoryId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AccountingCategoryErrors.GenericNotFound);
    }
}
