using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.AddAdvanceUsage;

/// <summary>
/// Records a single advance-usage line item during the reconciliation phase.
/// Only the requester may do this; the aggregate enforces phase, type and the
/// no-overspending invariant. Returns the new usage row's Id on success.
/// </summary>
public sealed record AddAdvanceUsageCommand(
    Guid BudgetRequestId,
    Guid UserId,
    DateTime SpentOn,
    decimal Amount,
    string Description,
    IReadOnlyList<Guid>? AttachmentIds = null) : IRequest<Result<Guid>>;

public sealed class AddAdvanceUsageCommandHandler
    : IRequestHandler<AddAdvanceUsageCommand, Result<Guid>>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddAdvanceUsageCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUnitOfWork unitOfWork)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(
        AddAdvanceUsageCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result<Guid>.Failure("Budget request not found.");

        if (command.UserId != budgetRequest.RequesterId)
            return Result<Guid>.Failure("Only the requester can record usage on their own advance.");

        var result = budgetRequest.AddAdvanceUsage(
            DateTime.SpecifyKind(command.SpentOn, DateTimeKind.Utc),
            command.Amount,
            command.Description,
            command.AttachmentIds,
            command.UserId,
            DateTime.UtcNow);

        if (result.IsFailure) return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return result;
    }
}
