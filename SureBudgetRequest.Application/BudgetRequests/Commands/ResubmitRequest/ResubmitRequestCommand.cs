using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;

    public ResubmitRequestCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository,
        ICurrencyRepository currencyRepository,
        IWithdrawMethodRepository withdrawMethodRepository,
        IUnitOfWork unitOfWork,
        INotificationDispatcher dispatcher)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _currencyRepository = currencyRepository;
        _withdrawMethodRepository = withdrawMethodRepository;
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
            return Result.Failure("Budget request not found.");

        if (budgetRequest.RequesterId != command.RequesterId)
            return Result.Failure("Only the requester can resubmit their request.");

        var requester = await _userRepository.GetByIdAsync(command.RequesterId, cancellationToken);
        if (requester is null)
            return Result.Failure("Requester not found.");

        var department = await _departmentRepository.GetByIdAsync(
            requester.DepartmentId, cancellationToken);
        if (department is null)
            return Result.Failure("Requester's department not found.");

        if (department.HeadUserId is null)
            return Result.Failure("Your department has no head assigned. Contact admin before resubmitting.");

        var deptHead = await _userRepository.GetByIdAsync(department.HeadUserId.Value, cancellationToken);
        if (deptHead is null)
            return Result.Failure("Department head not found.");

        var currency = await _currencyRepository.GetByCodeAsync(
            budgetRequest.CurrencyCode, cancellationToken);
        if (currency is null)
            return Result.Failure($"Currency '{budgetRequest.CurrencyCode}' not found.");
        if (!currency.IsActive)
            return Result.Failure($"Currency '{currency.Code}' is not active.");

        // Re-check monthly position at resubmission time — the dept's spend may have
        // changed since the original submission (other requests approved/paid in the
        // meantime). The justification is required again if still applicable.
        decimal? monthlySpendBeforeInMmk = null;
        if (department.MonthlyLimit.HasValue)
        {
            var nowUtc = DateTime.UtcNow;
            monthlySpendBeforeInMmk = await _budgetRequestRepository
                .GetMonthlyApprovedSpendInMmkAsync(
                    department.Id, nowUtc.Year, nowUtc.Month, cancellationToken);
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
                return Result.Failure("Selected withdraw method no longer exists.");
            if (!method.IsActive)
                return Result.Failure(
                    $"Withdraw method '{method.Name}' has been deactivated. " +
                    "Edit the draft and pick a different method before resubmitting.");
            methodRequiresAttachment = method.RequiresAttachment;
        }

        var previousStatus = budgetRequest.Status;
        var result = budgetRequest.ResubmitAfterSendBack(
            department.Id,
            department.BudgetLimit,
            department.MonthlyLimit,
            monthlySpendBeforeInMmk,
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
