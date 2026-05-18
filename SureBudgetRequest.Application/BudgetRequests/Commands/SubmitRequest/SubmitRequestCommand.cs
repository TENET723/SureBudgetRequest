using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
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
    private readonly INotificationDispatcher _dispatcher;

    public SubmitRequestCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository,
        ICurrencyRepository currencyRepository,
        IUnitOfWork unitOfWork,
        INotificationDispatcher dispatcher)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _currencyRepository = currencyRepository;
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
            return Result.Failure("Budget request not found.");

        if (budgetRequest.RequesterId != command.RequesterId)
            return Result.Failure("Only the requester can submit their request.");

        var requester = await _userRepository.GetByIdAsync(command.RequesterId, cancellationToken);
        if (requester is null)
            return Result.Failure("Requester not found.");

        var department = await _departmentRepository.GetByIdAsync(
            requester.DepartmentId, cancellationToken);
        if (department is null)
            return Result.Failure("Requester's department not found.");

        var currency = await _currencyRepository.GetByCodeAsync(
            budgetRequest.CurrencyCode, cancellationToken);
        if (currency is null)
            return Result.Failure($"Currency '{budgetRequest.CurrencyCode}' not found.");
        if (!currency.IsActive)
            return Result.Failure($"Currency '{currency.Code}' is not active.");

        if (department.HeadUserId is null)
            return Result.Failure("Your department has no head assigned. Contact admin before submitting.");

        var headUser = await _userRepository.GetByIdAsync(department.HeadUserId.Value, cancellationToken);
        if (headUser is null)
            return Result.Failure("Department head not found.");

        // Monthly spend lookup — only run when the dept has a monthly limit configured.
        // The "month" of a request is determined by SubmittedAt in UTC; we use today's
        // UTC year/month here because that's the calendar bucket this submission lands in.
        decimal? monthlySpendBeforeInMmk = null;
        if (department.MonthlyLimit.HasValue)
        {
            var nowUtc = DateTime.UtcNow;
            monthlySpendBeforeInMmk = await _budgetRequestRepository
                .GetMonthlyApprovedSpendInMmkAsync(
                    department.Id, nowUtc.Year, nowUtc.Month, cancellationToken);
        }

        var previousStatus = budgetRequest.Status;

        var result = budgetRequest.Submit(
            department.Id,
            department.BudgetLimit,
            department.MonthlyLimit,
            monthlySpendBeforeInMmk,
            currency.RateToMmk,
            department.HeadUserId.Value,
            headUser.FullName,
            requester.FullName);

        if (result.IsFailure) return result;

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
