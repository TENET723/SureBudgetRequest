using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.SendBackRequest;

public sealed record SendBackRequestCommand(
    Guid BudgetRequestId,
    Guid ApproverId,
    string Comment) : IRequest<Result>;

public sealed class SendBackRequestCommandHandler
    : IRequestHandler<SendBackRequestCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;

    public SendBackRequestCommandHandler(
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
        SendBackRequestCommand command,
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
            RequestStatus.PendingDeptHead   => approver.Role == UserRole.DepartmentHead,
            RequestStatus.PendingManagement => approver.Role == UserRole.Management,
            RequestStatus.PendingFinance    => approver.Role == UserRole.Finance,
            _ => false
        };

        if (!roleCheck)
            return Result.Failure(UserErrors.RoleUnauthorized(approver.Role.ToString(), budgetRequest.Status.ToString()));

        // Finance-stage gate: only Finance Approvers (Type 1) may send back.
        // Payer-only (Type 2) Finance users can record payments but cannot send back.
        if (budgetRequest.Status == RequestStatus.PendingFinance && !approver.IsFinanceApprover)
            return Result.Failure(BudgetRequestErrors.FinanceApproverRequired);

        var previousStatus = budgetRequest.Status;
        var result = budgetRequest.SendBack(command.ApproverId, command.Comment);
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
