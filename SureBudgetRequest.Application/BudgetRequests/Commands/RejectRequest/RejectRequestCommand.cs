using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.RejectRequest;

public sealed record RejectRequestCommand(
    Guid BudgetRequestId,
    Guid ApproverId,
    string Comment) : IRequest<Result>;

public sealed class RejectRequestCommandHandler
    : IRequestHandler<RejectRequestCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ICurrentUser? _currentUser;

    public RejectRequestCommandHandler(
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
        RejectRequestCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        var approver = await _userRepository.GetByIdAsync(command.ApproverId, cancellationToken);
        if (approver is null)
            return Result.Failure(UserErrors.NotFound(command.ApproverId));

        var roleCheck = budgetRequest.Status switch
        {
            RequestStatus.PendingDeptHead   => command.ApproverId == budgetRequest.DeptHeadIdAtSubmission,
            RequestStatus.PendingManagement => approver.Role == UserRole.Management,
            RequestStatus.PendingFinance    => approver.Role == UserRole.Finance,
            _ => false
        };

        if (!roleCheck)
            return Result.Failure(UserErrors.RoleUnauthorized(approver.Role.ToString(), budgetRequest.Status.ToString()));

        // Finance-stage gate: only Finance Approvers (Type 1) may reject.
        if (budgetRequest.Status == RequestStatus.PendingFinance && !approver.IsFinanceApprover)
            return Result.Failure(BudgetRequestErrors.FinanceApproverRequired);

        var previousStatus = budgetRequest.Status;
        var result = budgetRequest.Reject(command.ApproverId, command.Comment);
        if (result.IsFailure) return result;

        var actorName = _currentUser?.IsFallbackPersonAct == true
            ? $"{_currentUser.RealActorFullName} (acting for {approver.FullName})"
            : approver.FullName;

        await _dispatcher.DispatchAsync(
            budgetRequest,
            previousStatus,
            command.ApproverId,
            actorName: actorName,
            comment: command.Comment,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
