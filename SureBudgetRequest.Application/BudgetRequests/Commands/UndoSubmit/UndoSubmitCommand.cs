using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.UndoSubmit;

public sealed record UndoSubmitCommand(
    Guid BudgetRequestId,
    Guid RequesterId) : IRequest<Result>;

public sealed class UndoSubmitCommandHandler
    : IRequestHandler<UndoSubmitCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ICurrentUser? _currentUser;

    public UndoSubmitCommandHandler(
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
        UndoSubmitCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        if (budgetRequest.RequesterId != command.RequesterId)
            return Result.Failure(BudgetRequestErrors.OnlyRequesterCanUndoSubmit);

        var requester = await _userRepository.GetByIdAsync(command.RequesterId, cancellationToken);
        if (requester is null)
            return Result.Failure(UserErrors.NotFound(command.RequesterId));

        var previousStatus = budgetRequest.Status;
        var result = budgetRequest.UndoSubmit(command.RequesterId);
        if (result.IsFailure) return result;

        var actorName = _currentUser?.IsFallbackPersonAct == true
            ? $"{_currentUser.RealActorFullName} (acting for {requester.FullName})"
            : requester.FullName;

        await _dispatcher.DispatchSubmissionUndoneAsync(
            budgetRequest,
            previousStatus,
            actorName,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
