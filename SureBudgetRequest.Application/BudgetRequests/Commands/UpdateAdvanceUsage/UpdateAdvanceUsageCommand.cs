using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.UpdateAdvanceUsage;

/// <summary>
/// Edits an existing advance-usage line item while reconciliation is pending.
/// Requester-only; the aggregate re-checks the no-overspending invariant.
/// </summary>
public sealed record UpdateAdvanceUsageCommand(
    Guid BudgetRequestId,
    Guid UsageId,
    Guid UserId,
    DateTime SpentOn,
    decimal Amount,
    string Description,
    Guid? AttachmentId) : IRequest<Result>;

public sealed class UpdateAdvanceUsageCommandHandler
    : IRequestHandler<UpdateAdvanceUsageCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAdvanceUsageCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUnitOfWork unitOfWork)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateAdvanceUsageCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure("Budget request not found.");

        if (command.UserId != budgetRequest.RequesterId)
            return Result.Failure("Only the requester can edit usage on their own advance.");

        var result = budgetRequest.UpdateAdvanceUsage(
            command.UsageId,
            DateTime.SpecifyKind(command.SpentOn, DateTimeKind.Utc),
            command.Amount,
            command.Description,
            command.AttachmentId);

        if (result.IsFailure) return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
