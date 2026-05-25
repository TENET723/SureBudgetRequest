using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.SubmitReconciliation;

/// <summary>
/// Finalises the requester's reconciliation. Requester-only. Lands the request
/// in <c>Reconciled</c> (usage == advance) or <c>AwaitingRefund</c> (usage &lt;
/// advance) and fires the reconciliation-submitted notifications.
/// </summary>
public sealed record SubmitReconciliationCommand(
    Guid BudgetRequestId,
    Guid UserId) : IRequest<Result>;

public sealed class SubmitReconciliationCommandHandler
    : IRequestHandler<SubmitReconciliationCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;

    public SubmitReconciliationCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        INotificationDispatcher dispatcher)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
    }

    public async Task<Result> Handle(
        SubmitReconciliationCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        if (command.UserId != budgetRequest.RequesterId)
            return Result.Failure(BudgetRequestErrors.OnlyRequesterCanSubmitReconciliation);

        var result = budgetRequest.SubmitReconciliation(command.UserId, DateTime.UtcNow);
        if (result.IsFailure) return result;

        var actor = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);

        await _dispatcher.DispatchReconciliationSubmittedAsync(
            budgetRequest,
            actorName: actor?.FullName,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
