using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.RecordReimbursement;

/// <summary>
/// Records payment of the outstanding reimbursement on an advance that
/// reconciled for more than it disbursed. Finance-only. The amount must match
/// <c>BudgetRequest.ReimbursementAmount</c> exactly (the aggregate enforces this).
/// On success the advance becomes <c>Reconciled</c>.
/// </summary>
public sealed record RecordReimbursementCommand(
    Guid BudgetRequestId,
    Guid FinanceUserId,
    decimal Amount) : IRequest<Result>;

public sealed class RecordReimbursementCommandHandler
    : IRequestHandler<RecordReimbursementCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;

    public RecordReimbursementCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        INotificationDispatcher dispatcher)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
    }

    public async Task<Result> Handle(
        RecordReimbursementCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        var financeUser = await _userRepository.GetByIdAsync(command.FinanceUserId, cancellationToken);
        if (financeUser is null || financeUser.Role != UserRole.Finance)
            return Result.Failure(BudgetRequestErrors.OnlyFinanceCanRecordReimbursement);

        var result = budgetRequest.RecordReimbursement(
            command.Amount,
            command.FinanceUserId);

        if (result.IsFailure) return result;

        await _dispatcher.DispatchReimbursementRecordedAsync(
            budgetRequest,
            actorName: financeUser.FullName,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
