using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.UndoReconciliationSubmit;

/// <summary>
/// Reverts a submitted reconciliation back to PendingReconciliation so the
/// requester can adjust, add, or remove usage line items.
/// Permitted for Finance or an Admin only.
/// </summary>
public sealed record UndoReconciliationSubmitCommand(
    Guid BudgetRequestId,
    Guid UserId) : IRequest<Result>;

public sealed class UndoReconciliationSubmitCommandHandler
    : IRequestHandler<UndoReconciliationSubmitCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ICurrentUser? _currentUser;

    public UndoReconciliationSubmitCommandHandler(
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
        UndoReconciliationSubmitCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        var actor = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (actor is null)
            return Result.Failure(UserErrors.NotFound(command.UserId));

        var isFinance = actor.Role == UserRole.Finance;
        var isAdmin = actor.Role == UserRole.Admin;

        if (!isFinance && !isAdmin)
            return Result.Failure(BudgetRequestErrors.OnlyFinanceCanUndoReconciliation);

        var result = budgetRequest.UndoReconciliationSubmit(command.UserId);
        if (result.IsFailure) return result;

        var actorName = _currentUser?.IsFallbackPersonAct == true
            ? $"{_currentUser.RealActorFullName} (acting for {actor.FullName})"
            : actor.FullName;

        await _dispatcher.DispatchReconciliationSubmissionUndoneAsync(
            budgetRequest,
            actorName,
            isFinanceActor: isFinance,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
