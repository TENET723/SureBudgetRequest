using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.CancelRequest;

public sealed record CancelRequestCommand(
    Guid BudgetRequestId,
    Guid RequesterId) : IRequest<Result>;

public sealed class CancelRequestCommandHandler
    : IRequestHandler<CancelRequestCommand, Result>
{
    private readonly IBudgetRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelRequestCommandHandler(
        IBudgetRequestRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        CancelRequestCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _repository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure("Budget request not found.");

        var result = budgetRequest.Cancel(command.RequesterId);
        if (result.IsFailure) return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
