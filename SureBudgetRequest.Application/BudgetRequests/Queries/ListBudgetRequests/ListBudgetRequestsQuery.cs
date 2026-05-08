using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.ListBudgetRequests;

public sealed record ListBudgetRequestsQuery(
    Guid? RequesterId = null,
    Guid? DepartmentId = null,
    RequestStatus? Status = null,
    IReadOnlyCollection<RequestStatus>? Statuses = null) : IRequest<Result<IReadOnlyList<BudgetRequestSummaryDto>>>;

public sealed class ListBudgetRequestsQueryHandler
    : IRequestHandler<ListBudgetRequestsQuery, Result<IReadOnlyList<BudgetRequestSummaryDto>>>
{
    private readonly IBudgetRequestRepository _repository;

    public ListBudgetRequestsQueryHandler(IBudgetRequestRepository repository)
        => _repository = repository;

    public async Task<Result<IReadOnlyList<BudgetRequestSummaryDto>>> Handle(
        ListBudgetRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var entities = await _repository.ListAsync(
            request.RequesterId,
            request.DepartmentId,
            request.Status,
            request.Statuses,
            cancellationToken);

        var dtos = entities
            .Select(BudgetRequestSummaryDto.FromEntity)
            .ToList();

        return Result.Success<IReadOnlyList<BudgetRequestSummaryDto>>(dtos);
    }
}
