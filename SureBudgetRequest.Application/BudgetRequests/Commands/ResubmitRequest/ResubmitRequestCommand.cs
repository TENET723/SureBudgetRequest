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
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;

    public ResubmitRequestCommandHandler(
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

        var previousStatus = budgetRequest.Status;
        var result = budgetRequest.ResubmitAfterSendBack(
            department.Id,
            department.BudgetLimit,
            currency.RateToMmk,
            deptHead.Id,
            deptHead.FullName,
            requester.FullName);

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
