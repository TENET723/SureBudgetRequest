using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.SubmitRequest;

public sealed record SubmitRequestCommand(
    Guid BudgetRequestId,
    Guid RequesterId) : IRequest<Result>;

public sealed class SubmitRequestCommandHandler
    : IRequestHandler<SubmitRequestCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IWithdrawMethodRepository _withdrawMethodRepository;
    private readonly IBudgetRequestModificationRepository _modificationRepository;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;

    public SubmitRequestCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository,
        ICurrencyRepository currencyRepository,
        IWithdrawMethodRepository withdrawMethodRepository,
        IBudgetRequestModificationRepository modificationRepository,
        IDateTimeProvider clock,
        IUnitOfWork unitOfWork,
        INotificationDispatcher dispatcher)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _currencyRepository = currencyRepository;
        _withdrawMethodRepository = withdrawMethodRepository;
        _modificationRepository = modificationRepository;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
    }

    public async Task<Result> Handle(
        SubmitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        if (budgetRequest.RequesterId != command.RequesterId)
            return Result.Failure(BudgetRequestErrors.OnlyRequesterCanSubmit);

        // Block a new Advance when the requester still has an overdue advance
        // (past its reconciliation deadline). Cheap early exit before the
        // requester/department loads. Other request types are unaffected.
        if (budgetRequest.Type == BudgetRequestType.Advance
            && await _budgetRequestRepository.HasOverdueAdvanceAsync(command.RequesterId, DateTime.UtcNow, cancellationToken))
        {
            return Result.Failure(BudgetRequestErrors.AdvanceBlockedByOverdueReconciliation);
        }

        var requester = await _userRepository.GetByIdAsync(command.RequesterId, cancellationToken);
        if (requester is null)
            return Result.Failure(UserErrors.NotFound(command.RequesterId));

        var department = await _departmentRepository.GetByIdAsync(
            requester.DepartmentId, cancellationToken);
        if (department is null)
            return Result.Failure(BudgetRequestErrors.DepartmentNotFound);

        var currency = await _currencyRepository.GetByCodeAsync(
            budgetRequest.CurrencyCode, cancellationToken);
        if (currency is null)
            return Result.Failure(CurrencyErrors.NotFound(budgetRequest.CurrencyCode));
        if (!currency.IsActive)
            return Result.Failure(CurrencyErrors.Inactive(currency.Code));

        if (department.HeadUserId is null)
            return Result.Failure(BudgetRequestErrors.DepartmentHeadNotAssigned);

        var headUser = await _userRepository.GetByIdAsync(department.HeadUserId.Value, cancellationToken);
        if (headUser is null)
            return Result.Failure(BudgetRequestErrors.DepartmentHeadNotFound);

        // Compute the current calendar-month window and approved spend so far.
        var (monthStartUtc, monthEndUtc) = MonthlyBudgetWindow.CurrentUtc(
            _clock.BusinessNow, _clock.BusinessUtcOffset);
        var monthlySpendBefore = await _budgetRequestRepository.GetApprovedSpendInMmkAsync(
            department.Id, monthStartUtc, monthEndUtc, cancellationToken);

        // Resolve the chosen withdraw method so the domain can enforce its
        // RequiresAttachment invariant. The method must exist by this point —
        // CreateDraft / UpdateDraft already gate it; the entity guard re-checks
        // WithdrawMethodId is non-null inside Submit.
        bool methodRequiresAttachment = false;
        if (budgetRequest.WithdrawMethodId.HasValue)
        {
            var method = await _withdrawMethodRepository.GetByIdAsync(
                budgetRequest.WithdrawMethodId.Value, cancellationToken);
            if (method is null)
                return Result.Failure(WithdrawMethodErrors.NotFound);
            if (!method.IsActive)
                return Result.Failure(WithdrawMethodErrors.Inactive(method.Name));
            methodRequiresAttachment = method.RequiresAttachment;
        }

        var previousStatus = budgetRequest.Status;

        var result = budgetRequest.Submit(
            department.Id,
            department.BudgetLimit,
            department.MonthlyLimit,
            monthlySpendBefore,
            monthStartUtc,
            monthEndUtc,
            currency.RateToMmk,
            department.HeadUserId.Value,
            headUser.FullName,
            requester.FullName,
            methodRequiresAttachment);

        if (result.IsFailure) return result;

        // Record the submission in the audit trail.
        await _modificationRepository.AddAsync(
            new BudgetRequestModification(budgetRequest.Id, command.RequesterId),
            cancellationToken);

        // Dispatch FIRST so the outbox entry joins the same transaction.
        // On submissions the actor is the requester — actorName left null
        // because the builder uses RequesterName for the "Submitted by" field.
        await _dispatcher.DispatchAsync(
            budgetRequest,
            previousStatus,
            command.RequesterId,
            actorName: null,
            comment: null,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
