using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.RecordPayment;

public sealed record RecordPaymentCommand(
    Guid BudgetRequestId,
    Guid FinanceUserId,
    decimal Amount,
    DateTime PaidAt,
    string? Reference,
    string? Note) : IRequest<Result>;

public sealed class RecordPaymentCommandHandler
    : IRequestHandler<RecordPaymentCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public RecordPaymentCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        INotificationService notificationService)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(
        RecordPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure("Budget request not found.");

        var financeUser = await _userRepository.GetByIdAsync(command.FinanceUserId, cancellationToken);
        if (financeUser is null || financeUser.Role != UserRole.Finance)
            return Result.Failure("Only a Finance user can record payments.");

        var previousStatus = budgetRequest.Status;
        var result = budgetRequest.RecordPayment(
            command.Amount,
            command.FinanceUserId,
            command.PaidAt,
            command.Reference,
            command.Note);

        if (result.IsFailure) return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Only notify when fully paid (status changed to Paid)
        if (budgetRequest.Status == RequestStatus.Paid)
        {
            await NotificationDispatcher.DispatchAsync(
                budgetRequest,
                previousStatus,
                command.FinanceUserId,
                comment: null,
                _notificationService,
                cancellationToken);
        }

        return Result.Success();
    }
}
