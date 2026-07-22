using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.RecordRefund;

/// <summary>
/// Records receipt of the outstanding refund on an advance that reconciled for
/// less than it disbursed. Finance-only. The amount must match
/// <c>BudgetRequest.RefundAmount</c> exactly (the aggregate enforces this).
/// On success the advance becomes <c>Reconciled</c>.
/// </summary>
public sealed record RecordRefundCommand(
    Guid BudgetRequestId,
    Guid FinanceUserId,
    decimal Amount) : IRequest<Result>;

public sealed class RecordRefundCommandHandler
    : IRequestHandler<RecordRefundCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;

    public RecordRefundCommandHandler(
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
        RecordRefundCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        var financeUser = await _userRepository.GetByIdAsync(command.FinanceUserId, cancellationToken);
        if (financeUser is null || financeUser.Role != UserRole.Finance)
            return Result.Failure(BudgetRequestErrors.OnlyFinanceCanRecordRefund);

        var result = budgetRequest.RecordRefund(
            command.Amount,
            command.FinanceUserId);

        if (result.IsFailure) return result;

        await _dispatcher.DispatchRefundRecordedAsync(
            budgetRequest,
            actorName: financeUser.FullName,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
