using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.RemoveAdvanceUsage;

/// <summary>
/// Removes an advance-usage line item while reconciliation is pending.
/// Requester-only.
/// </summary>
public sealed record RemoveAdvanceUsageCommand(
    Guid BudgetRequestId,
    Guid UsageId,
    Guid UserId) : IRequest<Result>;

public sealed class RemoveAdvanceUsageCommandHandler
    : IRequestHandler<RemoveAdvanceUsageCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveAdvanceUsageCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUnitOfWork unitOfWork)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        RemoveAdvanceUsageCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure("Budget request not found.");

        if (command.UserId != budgetRequest.RequesterId)
            return Result.Failure("Only the requester can remove usage from their own advance.");

        var result = budgetRequest.RemoveAdvanceUsage(command.UsageId);
        if (result.IsFailure) return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
