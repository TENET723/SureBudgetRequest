using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.ApproveRequest;

public sealed record ApproveRequestCommand(
    Guid BudgetRequestId,
    Guid ApproverId,
    Guid? CoaId = null,
    // Finance-stage only. Required for Advance requests, must be null otherwise —
    // the aggregate enforces the Type-vs-deadline coupling, not this layer.
    DateTime? ReconciliationDeadline = null) : IRequest<Result>;

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

        // Finance-stage gate: only Finance Approvers (Type 1) may approve.
        // Payer-only (Type 2) Finance users can record payments but cannot approve.
        if (budgetRequest.Status == RequestStatus.PendingFinance && !approver.IsFinanceApprover)
            return Result.Failure(Error.Forbidden("BudgetRequest.FinanceUserRestricted", "This Finance user is restricted to recording payments and cannot approve requests."));

        // Finance-stage gate: validate the supplied CoaId exists and is active
        // BEFORE handing off to the domain. The domain only enforces "non-null."
        if (budgetRequest.Status == RequestStatus.PendingFinance)
        {
            if (!command.CoaId.HasValue || command.CoaId.Value == Guid.Empty)
                return Result.Failure(BudgetRequestErrors.CoaRequiredForFinanceApproval);

            var coa = await _coaRepository.GetByIdAsync(command.CoaId.Value, cancellationToken);
            if (coa is null)
                return Result.Failure(BudgetRequestErrors.CoaNotFound);
            if (!coa.IsActive)
                return Result.Failure(BudgetRequestErrors.CoaDeactivated(coa.Code));
        }

        // Normalise the (date-picker-sourced) deadline to UTC kind before it
        // crosses into the domain — Npgsql requires UTC for timestamptz columns.
        var reconciliationDeadline = command.ReconciliationDeadline.HasValue
            ? DateTime.SpecifyKind(command.ReconciliationDeadline.Value, DateTimeKind.Utc)
            : (DateTime?)null;

        var previousStatus = budgetRequest.Status;
        var result = budgetRequest.ApproveBy(command.ApproverId, command.CoaId, reconciliationDeadline);
        if (result.IsFailure) return result;

        await _dispatcher.DispatchAsync(
            budgetRequest,
            previousStatus,
            command.ApproverId,
            actorName: approver.FullName,
            comment: null,
            cancellationToken);

        // TODO: deadline reminder background job (T-3 / T-1 / overdue) — when an
        // advance is approved here it gets a ReconciliationDeadline; a hosted
        // service should later poll for advances approaching/past their deadline
        // and notify the requester. Out of scope for this task.

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
