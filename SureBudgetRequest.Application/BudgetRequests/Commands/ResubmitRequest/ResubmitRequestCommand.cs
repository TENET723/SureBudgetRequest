using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.ResubmitRequest;

public sealed record ResubmitRequestCommand(
    Guid BudgetRequestId,
    Guid RequesterId) : IRequest<Result>;

public sealed class ResubmitRequestCommandHandler
    : IRequestHandler<ResubmitRequestCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IWithdrawMethodRepository _withdrawMethodRepository;
    private readonly IDepartmentBudgetPeriodRepository _periodRepository;
    private readonly IFinancialYearProvider _financialYearProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;

    public ResubmitRequestCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository,
        ICurrencyRepository currencyRepository,
        IWithdrawMethodRepository withdrawMethodRepository,
        IDepartmentBudgetPeriodRepository periodRepository,
        IFinancialYearProvider financialYearProvider,
        IUnitOfWork unitOfWork,
        INotificationDispatcher dispatcher)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _currencyRepository = currencyRepository;
        _withdrawMethodRepository = withdrawMethodRepository;
        _periodRepository = periodRepository;
        _financialYearProvider = financialYearProvider;
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
    }

    public async Task<Result> Handle(
        ResubmitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        if (budgetRequest.RequesterId != command.RequesterId)
            return Result.Failure(BudgetRequestErrors.OnlyRequesterCanResubmit);

        var requester = await _userRepository.GetByIdAsync(command.RequesterId, cancellationToken);
        if (requester is null)
            return Result.Failure(UserErrors.NotFound(command.RequesterId));

        var department = await _departmentRepository.GetByIdAsync(
            requester.DepartmentId, cancellationToken);
        if (department is null)
            return Result.Failure(BudgetRequestErrors.DepartmentNotFound);

        if (department.HeadUserId is null)
            return Result.Failure(BudgetRequestErrors.DepartmentHeadNotAssigned);

        var deptHead = await _userRepository.GetByIdAsync(department.HeadUserId.Value, cancellationToken);
        if (deptHead is null)
            return Result.Failure(BudgetRequestErrors.DepartmentHeadNotFound);

        var currency = await _currencyRepository.GetByCodeAsync(
            budgetRequest.CurrencyCode, cancellationToken);
        if (currency is null)
            return Result.Failure(CurrencyErrors.NotFound(budgetRequest.CurrencyCode));
        if (!currency.IsActive)
            return Result.Failure(CurrencyErrors.Inactive(currency.Code));

        // Re-check the period position at resubmission time — the dept's spend may
        // have changed since the original submission (other requests approved/paid
        // in the meantime), and the active config may have rolled into a new FY.
        // The justification is required again if still applicable.
        var currentFy = await _financialYearProvider.GetCurrentFinancialYearAsync(cancellationToken);
        var activePeriod = await _periodRepository.GetActiveAsync(department.Id, currentFy, cancellationToken);
        var periodType = activePeriod?.PeriodType ?? BudgetPeriodType.None;

        decimal? periodLimit = null;
        decimal? periodSpendBeforeInMmk = null;
        DateTime? periodStartUtc = null;
        DateTime? periodEndUtc = null;
        if (periodType != BudgetPeriodType.None)
        {
            var (startUtc, endUtc) = await _financialYearProvider
                .GetPeriodWindowUtcAsync(periodType, cancellationToken);
            periodStartUtc = startUtc;
            periodEndUtc = endUtc;
            periodLimit = activePeriod!.LimitAmount;
            periodSpendBeforeInMmk = await _budgetRequestRepository
                .GetApprovedSpendInMmkAsync(department.Id, startUtc, endUtc, cancellationToken);
        }

        // Same withdraw-method check as Submit. A method may have been
        // deactivated or had its RequiresAttachment toggled in the gap between
        // first submission and resubmission.
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
        var result = budgetRequest.ResubmitAfterSendBack(
            department.Id,
            department.BudgetLimit,
            periodType,
            periodLimit,
            periodSpendBeforeInMmk,
            periodStartUtc,
            periodEndUtc,
            currency.RateToMmk,
            deptHead.Id,
            deptHead.FullName,
            requester.FullName,
            methodRequiresAttachment);

        if (result.IsFailure) return result;

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
