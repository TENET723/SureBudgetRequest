using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public SubmitRequestCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository,
        ICurrencyRepository currencyRepository,
        IUnitOfWork unitOfWork,
        INotificationService notificationService)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _currencyRepository = currencyRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(
        SubmitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure("Budget request not found.");

        if (budgetRequest.RequesterId != command.RequesterId)
            return Result.Failure("Only the requester can submit their request.");

        // Load the requester's current department (R12: snapshot dept head at submission)
        var requester = await _userRepository.GetByIdAsync(command.RequesterId, cancellationToken);
        if (requester is null)
            return Result.Failure("Requester not found.");

        var department = await _departmentRepository.GetByIdAsync(
            requester.DepartmentId, cancellationToken);
        if (department is null)
            return Result.Failure("Requester's department not found.");

        // Look up the current exchange rate for the draft's currency.
        var currency = await _currencyRepository.GetByCodeAsync(
            budgetRequest.CurrencyCode, cancellationToken);
        if (currency is null)
            return Result.Failure($"Currency '{budgetRequest.CurrencyCode}' not found.");
        if (!currency.IsActive)
            return Result.Failure($"Currency '{currency.Code}' is not active.");

        // Determine whether we need the Boss (R6, R7) — comparison happens in MMK.
        var amountInMmk = budgetRequest.RequestedAmount * currency.RateToMmk;
        var isOverLimit = amountInMmk > department.BudgetLimit;

        Guid? bossId = null;
        string? bossName = null;
        if (isOverLimit)
        {
            var boss = await _userRepository.FindBossAsync(cancellationToken);
            if (boss is null)
                return Result.Failure("No Boss is assigned in the system. Cannot submit over-limit request.");
            bossId = boss.Id;
            bossName = boss.FullName;
        }

        // A department may exist without a head (vacancy/bootstrap). Block submit until one is assigned.
        if (department.HeadUserId is null)
            return Result.Failure("Your department has no head assigned. Contact admin before submitting.");

        var headUser = await _userRepository.GetByIdAsync(department.HeadUserId.Value, cancellationToken);
        if (headUser is null)
            return Result.Failure("Department head not found.");

        var previousStatus = budgetRequest.Status;

        // Domain method: snapshots routing context + rate, fast-forwards through auto-approvals (R9)
        var result = budgetRequest.Submit(
            department.Id,
            department.BudgetLimit,
            currency.RateToMmk,
            department.HeadUserId.Value,
            headUser.FullName,
            bossId,
            bossName,
            requester.FullName);

        if (result.IsFailure) return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fire the appropriate Slack notification (§9)
        await NotificationDispatcher.DispatchAsync(
            budgetRequest,
            previousStatus,
            command.RequesterId,
            comment: null,
            _notificationService,
            cancellationToken);

        return Result.Success();
    }
}
