using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.ApproveRequest;

public sealed record ApproveRequestCommand(
    Guid BudgetRequestId,
    Guid ApproverId) : IRequest<Result>;

public sealed class ApproveRequestCommandHandler
    : IRequestHandler<ApproveRequestCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public ApproveRequestCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        INotificationService notificationService)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(
        ApproveRequestCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure("Budget request not found.");

        // Application-layer role check (Domain checks assignee ID; Application checks role)
        var approver = await _userRepository.GetByIdAsync(command.ApproverId, cancellationToken);
        if (approver is null)
            return Result.Failure("Approver not found.");

        var roleCheck = budgetRequest.Status switch
        {
            RequestStatus.PendingDeptHead   => approver.Role == UserRole.DepartmentHead,
            RequestStatus.PendingManagement => approver.Role == UserRole.Management,
            RequestStatus.PendingFinance    => approver.Role == UserRole.Finance,
            _ => false
        };

        if (!roleCheck)
            return Result.Failure($"User with role '{approver.Role}' cannot approve at the current stage '{budgetRequest.Status}'.");

        var previousStatus = budgetRequest.Status;
        var result = budgetRequest.ApproveBy(command.ApproverId);
        if (result.IsFailure) return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotificationDispatcher.DispatchAsync(
            budgetRequest,
            previousStatus,
            command.ApproverId,
            comment: null,
            _notificationService,
            cancellationToken);

        return Result.Success();
    }
}
