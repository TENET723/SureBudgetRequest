using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.UndoApproval;

public sealed record UndoApprovalCommand(
    Guid BudgetRequestId,
    Guid ApproverId,
    string? Comment = null) : IRequest<Result>;

public sealed class UndoApprovalCommandHandler
    : IRequestHandler<UndoApprovalCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ICurrentUser? _currentUser;

    public UndoApprovalCommandHandler(
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
        UndoApprovalCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        var approver = await _userRepository.GetByIdAsync(command.ApproverId, cancellationToken);
        if (approver is null)
            return Result.Failure(UserErrors.NotFound(command.ApproverId));

        var requiresManagement = budgetRequest.RequestedAmountInMmkAtSubmission > budgetRequest.DepartmentLimitAtSubmission;

        var isAuthorized = budgetRequest.Status switch
        {
            RequestStatus.PendingManagement =>
                command.ApproverId == budgetRequest.DeptHeadIdAtSubmission || approver.Role == UserRole.Admin,

            RequestStatus.PendingFinance =>
                requiresManagement
                    ? approver.Role is UserRole.Management or UserRole.Admin
                    : command.ApproverId == budgetRequest.DeptHeadIdAtSubmission || approver.Role == UserRole.Admin,

            RequestStatus.Approved =>
                approver.Role == UserRole.Admin || (approver.Role == UserRole.Finance && approver.IsFinanceApprover),

            _ => false
        };

        if (!isAuthorized)
            return Result.Failure(UserErrors.RoleUnauthorized(approver.Role.ToString(), budgetRequest.Status.ToString()));

        var stageName = budgetRequest.Status switch
        {
            RequestStatus.PendingManagement => "Department Head",
            RequestStatus.PendingFinance when requiresManagement => "Management",
            RequestStatus.PendingFinance => "Department Head",
            RequestStatus.Approved => "Finance",
            _ => "Approval"
        };

        var fallbackUserId = _currentUser?.IsFallbackPersonAct == true ? _currentUser.RealActorUserId : (Guid?)null;
        var result = budgetRequest.UndoApproval(command.ApproverId, command.Comment, fallbackUserId);
        if (result.IsFailure) return result;

        var actorName = _currentUser?.IsFallbackPersonAct == true
            ? $"{_currentUser.RealActorFullName} (acting for {approver.FullName})"
            : approver.FullName;

        await _dispatcher.DispatchApprovalUndoneAsync(
            budgetRequest,
            stageName,
            actorName,
            command.Comment,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
