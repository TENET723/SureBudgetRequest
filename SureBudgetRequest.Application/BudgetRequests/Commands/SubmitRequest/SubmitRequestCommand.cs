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
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public SubmitRequestCommandHandler(
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

        // Determine whether we need the Boss (R6, R7)
        var isOverLimit = budgetRequest.RequestedAmount > department.BudgetLimit;

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

        var headUser = await _userRepository.GetByIdAsync(department.HeadUserId, cancellationToken);
        if (headUser is null)
            return Result.Failure("Department head not found.");

        var previousStatus = budgetRequest.Status;

        // Domain method: snapshots routing context and fast-forwards through auto-approvals (R9)
        var result = budgetRequest.Submit(
            department.Id,
            department.BudgetLimit,
            department.HeadUserId,
            headUser.FullName,
            bossId,
            bossName,
            requester.FullName
            );

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
