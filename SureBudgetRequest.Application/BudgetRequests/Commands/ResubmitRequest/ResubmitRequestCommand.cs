using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public ResubmitRequestCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository,
        IUnitOfWork unitOfWork,
        INotificationService notificationService)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
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

        // A department may exist without a head (vacancy/bootstrap). Block resubmit until one is assigned.
        if (department.HeadUserId is null)
            return Result.Failure("Your department has no head assigned. Contact admin before resubmitting.");

        var deptHead = await _userRepository.GetByIdAsync(department.HeadUserId.Value, cancellationToken);
        if (deptHead is null)
            return Result.Failure("Department head not found.");

        var isOverLimit = budgetRequest.RequestedAmount > department.BudgetLimit;

        Guid? bossId = null;
        string? bossName = null;
        if (isOverLimit)
        {
            var boss = await _userRepository.FindBossAsync(cancellationToken);
            if (boss is null)
                return Result.Failure("No Boss is assigned. Cannot resubmit over-limit request.");
            bossId = boss.Id;
            bossName = boss.FullName;
        }

        var previousStatus = budgetRequest.Status;
        var result = budgetRequest.ResubmitAfterSendBack(
            department.Id,
            department.BudgetLimit,
            deptHead.Id,
            deptHead.FullName,
            bossId,
            bossName,
            requester.FullName);

        if (result.IsFailure) return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
