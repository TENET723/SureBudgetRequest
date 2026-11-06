using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.UndoRefund;

/// <summary>
/// Undoes a recorded refund on an advance request, moving the request from
/// Reconciled back to AwaitingRefund. Finance-only.
/// </summary>
public sealed record UndoRefundCommand(
    Guid BudgetRequestId,
    Guid FinanceUserId) : IRequest<Result>;

public sealed class UndoRefundCommandHandler
    : IRequestHandler<UndoRefundCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ICurrentUser? _currentUser;

    public UndoRefundCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        INotificationDispatcher dispatcher,
        ICurrentUser? currentUser = null)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(
        UndoRefundCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        var financeUser = await _userRepository.GetByIdAsync(command.FinanceUserId, cancellationToken);
        if (financeUser is null || financeUser.Role != UserRole.Finance)
            return Result.Failure(BudgetRequestErrors.OnlyFinanceCanUndoRefund);

        var result = budgetRequest.UndoRefund(command.FinanceUserId);
        if (result.IsFailure) return result;

        var actorName = _currentUser?.IsFallbackPersonAct == true
            ? $"{_currentUser.RealActorFullName} (acting for {financeUser.FullName})"
            : financeUser.FullName;

        await _dispatcher.DispatchRefundUndoneAsync(
            budgetRequest,
            actorName,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
