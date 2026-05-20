using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.ApproveRequest;

public sealed record ApproveRequestCommand(
    Guid BudgetRequestId,
    Guid ApproverId,
    Guid? CoaId = null) : IRequest<Result>;

public sealed class ApproveRequestCommandHandler
    : IRequestHandler<ApproveRequestCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICoaRepository _coaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;

    public ApproveRequestCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        ICoaRepository coaRepository,
        IUnitOfWork unitOfWork,
        INotificationDispatcher dispatcher)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _coaRepository = coaRepository;
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
    }

    public async Task<Result> Handle(
        ApproveRequestCommand command,
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
            return Result.Failure($"User with role '{approver.Role}' cannot approve at the current stage '{budgetRequest.Status}'.");

        // Finance-stage gate: only Finance Approvers (Type 1) may approve.
        // Payer-only (Type 2) Finance users can record payments but cannot approve.
        if (budgetRequest.Status == RequestStatus.PendingFinance && !approver.IsFinanceApprover)
            return Result.Failure("This Finance user is restricted to recording payments and cannot approve requests.");

        // Finance-stage gate: validate the supplied CoaId exists and is active
        // BEFORE handing off to the domain. The domain only enforces "non-null."
        if (budgetRequest.Status == RequestStatus.PendingFinance)
        {
            if (!command.CoaId.HasValue || command.CoaId.Value == Guid.Empty)
                return Result.Failure("Chart of Account is required when Finance approves.");

            var coa = await _coaRepository.GetByIdAsync(command.CoaId.Value, cancellationToken);
            if (coa is null)
                return Result.Failure("The selected Chart of Account no longer exists.");
            if (!coa.IsActive)
                return Result.Failure($"Chart of Account '{coa.Code}' has been deactivated and cannot be used.");
        }

        var previousStatus = budgetRequest.Status;
        var result = budgetRequest.ApproveBy(command.ApproverId, command.CoaId);
        if (result.IsFailure) return result;

        await _dispatcher.DispatchAsync(
            budgetRequest,
            previousStatus,
            command.ApproverId,
            actorName: approver.FullName,
            comment: null,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
