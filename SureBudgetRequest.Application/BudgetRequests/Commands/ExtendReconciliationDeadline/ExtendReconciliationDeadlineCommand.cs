using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.ExtendReconciliationDeadline;

/// <summary>
/// Pushes an advance's reconciliation deadline out. Finance-only. The aggregate
/// only allows extending (never shortening) the deadline.
/// </summary>
public sealed record ExtendReconciliationDeadlineCommand(
    Guid BudgetRequestId,
    Guid FinanceUserId,
    DateTime NewDeadline) : IRequest<Result>;

public sealed class ExtendReconciliationDeadlineCommandHandler
    : IRequestHandler<ExtendReconciliationDeadlineCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ExtendReconciliationDeadlineCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        ExtendReconciliationDeadlineCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure("Budget request not found.");

        var financeUser = await _userRepository.GetByIdAsync(command.FinanceUserId, cancellationToken);
        if (financeUser is null || financeUser.Role != UserRole.Finance)
            return Result.Failure("Only a Finance user can extend a reconciliation deadline.");

        var result = budgetRequest.ExtendReconciliationDeadline(
            DateTime.SpecifyKind(command.NewDeadline, DateTimeKind.Utc));

        if (result.IsFailure) return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
