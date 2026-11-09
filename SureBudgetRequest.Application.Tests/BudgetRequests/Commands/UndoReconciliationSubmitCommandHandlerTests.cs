using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.UndoReconciliationSubmit;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class UndoReconciliationSubmitCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();
    private readonly UndoReconciliationSubmitCommandHandler _handler;

    public UndoReconciliationSubmitCommandHandlerTests()
    {
        _handler = new UndoReconciliationSubmitCommandHandler(
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

    private static (BudgetRequest Request, User Requester) CreateAwaitingRefundAdvance()
    {
        var requesterId = Guid.NewGuid();
        var requester = CreateUserWithId(requesterId, UserRole.Employee);
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

        return (request, requester);
    }

    [Fact]
    public async Task Handle_WhenRequestNotFound_ReturnsNotFoundError()
    {
        var reqId = Guid.NewGuid();
        _budgetRequestRepository.GetByIdAsync(reqId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var result = await _handler.Handle(new UndoReconciliationSubmitCommand(reqId, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.NotFound(reqId));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenActorNotFound_ReturnsUserNotFoundError()
    {
        var (request, _) = CreateAwaitingRefundAdvance();
        var actorId = Guid.NewGuid();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(actorId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _handler.Handle(new UndoReconciliationSubmitCommand(request.Id, actorId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(actorId));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenActorIsUnrelatedEmployee_ReturnsOnlyFinanceError()
    {
        var (request, _) = CreateAwaitingRefundAdvance();
        var strangerId = Guid.NewGuid();
        var stranger = CreateUserWithId(strangerId, UserRole.Employee);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(stranger.Id, Arg.Any<CancellationToken>()).Returns(stranger);

        var result = await _handler.Handle(new UndoReconciliationSubmitCommand(request.Id, stranger.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyFinanceCanUndoReconciliation);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenActorIsRequester_ReturnsOnlyFinanceError()
    {
        var (request, requester) = CreateAwaitingRefundAdvance();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(requester.Id, Arg.Any<CancellationToken>()).Returns(requester);

        var result = await _handler.Handle(new UndoReconciliationSubmitCommand(request.Id, requester.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyFinanceCanUndoReconciliation);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenActorIsFinance_SucceedsAndDispatchesNotification()
    {
        var (request, _) = CreateAwaitingRefundAdvance();
        var finance = CreateUserWithId(Guid.NewGuid(), UserRole.Finance);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(finance.Id, Arg.Any<CancellationToken>()).Returns(finance);

        var result = await _handler.Handle(new UndoReconciliationSubmitCommand(request.Id, finance.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PendingReconciliation);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).DispatchReconciliationSubmissionUndoneAsync(request, finance.FullName, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenActorIsAdmin_SucceedsAndDispatchesNotification()
    {
        var (request, _) = CreateAwaitingRefundAdvance();
        var admin = CreateUserWithId(Guid.NewGuid(), UserRole.Admin);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);

        var result = await _handler.Handle(new UndoReconciliationSubmitCommand(request.Id, admin.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PendingReconciliation);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).DispatchReconciliationSubmissionUndoneAsync(request, admin.FullName, false, Arg.Any<CancellationToken>());
    }
}
