using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.UndoRefund;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class UndoRefundCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();
    private readonly UndoRefundCommandHandler _handler;

    public UndoRefundCommandHandlerTests()
    {
        _handler = new UndoRefundCommandHandler(
            _budgetRequestRepository,
            _userRepository,
            _unitOfWork,
            _dispatcher);
    }

    private static User CreateUserWithId(Guid id, UserRole role)
    {
        var user = new User("testuser", "Test User", $"test_{id:N}@example.com", Guid.NewGuid(), role);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        return user;
    }

    private static (BudgetRequest Request, User Finance) CreateReconciledAdvanceWithRefund()
    {
        var requesterId = Guid.NewGuid();
        var financeId = Guid.NewGuid();
        var requester = CreateUserWithId(requesterId, UserRole.Employee);
        var finance = CreateUserWithId(financeId, UserRole.Finance);
        var headId = Guid.NewGuid();

        var request = BudgetRequest.CreateDraft(
            requester.Id, requester.FullName, BudgetRequestType.Advance, headId, "Bob Head",
            DateTime.UtcNow.AddHours(6.5).Date.AddDays(2),
            500_000m, "MMK", "Advance Trip", requester.FullName, "Job", Guid.NewGuid(),
            false, null);

        request.Submit(
            Guid.NewGuid(), 1_000_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, headId, "Bob Head", requester.FullName, false, 1);

        request.ApproveBy(headId);
        request.ApproveBy(Guid.NewGuid(), coaId: Guid.NewGuid(), reconciliationDeadline: DateTime.UtcNow.AddDays(14));
        request.RecordPayment(500_000m, DateTime.UtcNow, Guid.NewGuid(), "ADV-PAY", "Disbursed");

        request.AddAdvanceUsage(DateTime.UtcNow, 400_000m, "Expenses", null, requester.Id, DateTime.UtcNow);
        request.SubmitReconciliation(requester.Id, DateTime.UtcNow);

        // Record refund of 100_000m -> moves to Reconciled
        request.RecordRefund(100_000m, finance.Id, DateTime.UtcNow);

        return (request, finance);
    }

    [Fact]
    public async Task Handle_WhenRequestNotFound_ReturnsNotFoundError()
    {
        var reqId = Guid.NewGuid();
        _budgetRequestRepository.GetByIdAsync(reqId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var result = await _handler.Handle(new UndoRefundCommand(reqId, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.NotFound(reqId));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFinanceUserNotFound_ReturnsOnlyFinanceCanUndoRefundError()
    {
        var (request, _) = CreateReconciledAdvanceWithRefund();
        var financeId = Guid.NewGuid();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _handler.Handle(new UndoRefundCommand(request.Id, financeId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyFinanceCanUndoRefund);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Management)]
    public async Task Handle_WhenUserIsNotFinance_ReturnsOnlyFinanceCanUndoRefundError(UserRole role)
    {
        var (request, _) = CreateReconciledAdvanceWithRefund();
        var nonFinanceId = Guid.NewGuid();
        var nonFinanceUser = CreateUserWithId(nonFinanceId, role);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(nonFinanceUser.Id, Arg.Any<CancellationToken>()).Returns(nonFinanceUser);

        var result = await _handler.Handle(new UndoRefundCommand(request.Id, nonFinanceUser.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyFinanceCanUndoRefund);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValidFinanceUser_UndoesRefundAndDispatchesNotification()
    {
        var (request, finance) = CreateReconciledAdvanceWithRefund();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(finance.Id, Arg.Any<CancellationToken>()).Returns(finance);

        var result = await _handler.Handle(new UndoRefundCommand(request.Id, finance.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.AwaitingRefund);
        request.RefundReceivedAt.Should().BeNull();
        request.RefundReceivedByUserId.Should().BeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).DispatchRefundUndoneAsync(request, finance.FullName, Arg.Any<CancellationToken>());
    }
}
