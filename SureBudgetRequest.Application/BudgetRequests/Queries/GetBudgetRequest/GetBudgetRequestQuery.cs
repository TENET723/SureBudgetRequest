using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.GetBudgetRequest;

public sealed record GetBudgetRequestQuery(Guid BudgetRequestId) : IRequest<Result<BudgetRequestDto>>;

public sealed class GetBudgetRequestQueryHandler
    : IRequestHandler<GetBudgetRequestQuery, Result<BudgetRequestDto>>
{
    private readonly IBudgetRequestRepository _repository;
    private readonly ICoaRepository _coaRepository;

    public GetBudgetRequestQueryHandler(
        IBudgetRequestRepository repository,
        ICoaRepository coaRepository)
    {
        _repository = repository;
        _coaRepository = coaRepository;
    }

    public async Task<Result<BudgetRequestDto>> Handle(
        GetBudgetRequestQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.BudgetRequestId, cancellationToken);
        if (entity is null)
            return Result.Failure<BudgetRequestDto>("Budget request not found.");

        // Resolve the assigned Coa for display (null when never approved or when
        // the row pre-dates the COA feature).
        var coa = entity.CoaId.HasValue
            ? await _coaRepository.GetByIdAsync(entity.CoaId.Value, cancellationToken)
            : null;

        return Result.Success(BudgetRequestDto.FromEntity(entity, coa));
    }
}
