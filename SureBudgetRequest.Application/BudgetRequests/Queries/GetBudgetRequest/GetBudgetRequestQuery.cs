using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.GetBudgetRequest;

public sealed record GetBudgetRequestQuery(Guid BudgetRequestId) : IRequest<Result<BudgetRequestDto>>;

public sealed class GetBudgetRequestQueryHandler
    : IRequestHandler<GetBudgetRequestQuery, Result<BudgetRequestDto>>
{
    private readonly IBudgetRequestRepository _repository;

    public GetBudgetRequestQueryHandler(IBudgetRequestRepository repository)
        => _repository = repository;

    public async Task<Result<BudgetRequestDto>> Handle(
        GetBudgetRequestQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.BudgetRequestId, cancellationToken);
        if (entity is null)
            return Result.Failure<BudgetRequestDto>("Budget request not found.");

        return Result.Success(BudgetRequestDto.FromEntity(entity));
    }
}
