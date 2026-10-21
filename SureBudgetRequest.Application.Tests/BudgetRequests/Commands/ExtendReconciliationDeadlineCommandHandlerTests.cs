using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.ExtendReconciliationDeadline;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class ExtendReconciliationDeadlineCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ExtendReconciliationDeadlineCommandHandler _handler;

    public ExtendReconciliationDeadlineCommandHandlerTests()
    {
        _handler = new ExtendReconciliationDeadlineCommandHandler(
            _budgetRequestRepository,
            _userRepository,
            _unitOfWork);
    }

    private static BudgetRequest CreateApprovedAdvanceWithDeadline(DateTime initialDeadline)
    {
        var headId = Guid.NewGuid();
        var request = BudgetRequest.CreateDraft(
            Guid.NewGuid(), "Alice", BudgetRequestType.Advance, headId, "Bob Head",
            DateTime.UtcNow.AddHours(6.5).Date.AddDays(2),
            500_000m, "MMK", "Advance Trip", "Alice", "Job", Guid.NewGuid(),
            false, null);

        request.Submit(
            Guid.NewGuid(), 1_000_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, headId, "Bob Head", "Alice", false, 1);

        request.ApproveBy(headId);
        request.ApproveBy(Guid.NewGuid(), coaId: Guid.NewGuid(), reconciliationDeadline: initialDeadline);

        // Record full payment to transition into PendingReconciliation
        request.RecordPayment(500_000m, DateTime.UtcNow, Guid.NewGuid(), "ADV-01", "Disbursed");

        return request;
    }

    [Fact]
    public async Task Handle_WhenBudgetRequestNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        _budgetRequestRepository.GetByIdAsync(reqId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var command = new ExtendReconciliationDeadlineCommand(reqId, Guid.NewGuid(), DateTime.UtcNow.AddDays(20));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.NotFound(reqId));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFinanceUserNotFound_ReturnsOnlyFinanceCanExtendError()
    {
        // Arrange
        var initialDeadline = DateTime.UtcNow.AddDays(10);
        var request = CreateApprovedAdvanceWithDeadline(initialDeadline);
        var financeUserId = Guid.NewGuid();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new ExtendReconciliationDeadlineCommand(request.Id, financeUserId, DateTime.UtcNow.AddDays(20));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyFinanceCanExtend);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.DepartmentHead)]
    [InlineData(UserRole.Management)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Accounting)]
    [InlineData(UserRole.Support)]
    public async Task Handle_WhenUserIsNotFinanceRole_ReturnsOnlyFinanceCanExtendError(UserRole nonFinanceRole)
    {
        // Arrange
        var initialDeadline = DateTime.UtcNow.AddDays(10);
        var request = CreateApprovedAdvanceWithDeadline(initialDeadline);
        var userId = Guid.NewGuid();
        var nonFinanceUser = new User("user", "User", "user@sure.com", Guid.NewGuid(), nonFinanceRole);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(nonFinanceUser);

        var command = new ExtendReconciliationDeadlineCommand(request.Id, userId, DateTime.UtcNow.AddDays(20));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyFinanceCanExtend);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenShorteningDeadline_ReturnsDomainFailureWithoutSaving()
    {
        // Arrange
        var initialDeadline = DateTime.UtcNow.AddDays(14);
        var request = CreateApprovedAdvanceWithDeadline(initialDeadline);
        var financeUserId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance User", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns(financeUser);

        var earlierDeadline = DateTime.UtcNow.AddDays(7); // Shortening!
        var command = new ExtendReconciliationDeadlineCommand(request.Id, financeUserId, earlierDeadline);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("The new deadline must be later than the current deadline.");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValid_ExtendsDeadlineSavesAndReturnsSuccess()
    {
        // Arrange
        var initialDeadline = DateTime.UtcNow.AddDays(14);
        var request = CreateApprovedAdvanceWithDeadline(initialDeadline);
        var financeUserId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance User", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns(financeUser);

        var newDeadline = DateTime.Now.AddDays(28); // local / unspecified
        var command = new ExtendReconciliationDeadlineCommand(request.Id, financeUserId, newDeadline);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.ReconciliationDeadline.Should().NotBeNull();
        request.ReconciliationDeadline!.Value.Kind.Should().Be(DateTimeKind.Utc);
        request.ReconciliationDeadline.Value.Should().BeAfter(initialDeadline);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var initialDeadline = DateTime.UtcNow.AddDays(14);
        var request = CreateApprovedAdvanceWithDeadline(initialDeadline);
        var financeUserId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance User", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _budgetRequestRepository.GetByIdAsync(request.Id, ct).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, ct).Returns(financeUser);

        var command = new ExtendReconciliationDeadlineCommand(request.Id, financeUserId, DateTime.UtcNow.AddDays(25));

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _budgetRequestRepository.Received(1).GetByIdAsync(request.Id, ct);
        await _userRepository.Received(1).GetByIdAsync(financeUserId, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
