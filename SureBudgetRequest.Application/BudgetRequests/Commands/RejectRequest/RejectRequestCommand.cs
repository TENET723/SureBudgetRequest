using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

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

    public RejectRequestCommandHandler(
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
        RejectRequestCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure("Budget request not found.");

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
            return Result.Failure($"User with role '{approver.Role}' cannot reject at the current stage '{budgetRequest.Status}'.");

        var previousStatus = budgetRequest.Status;
        var result = budgetRequest.Reject(command.ApproverId, command.Comment);
        if (result.IsFailure) return result;

        await _dispatcher.DispatchAsync(
            budgetRequest,
            previousStatus,
            command.ApproverId,
            actorName: approver.FullName,
            comment: command.Comment,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
