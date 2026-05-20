using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.WithdrawMethods.Queries;

public sealed record WithdrawMethodDto(
    Guid Id,
    string Name,
    bool IsActive,
    DateTime CreatedAt)
{
    public static WithdrawMethodDto FromEntity(WithdrawMethod m) =>
        new(m.Id, m.Name, m.IsActive, m.CreatedAt);
}

// ── Get single ─────────────────────────────────────────────────────────────

public sealed record GetWithdrawMethodQuery(Guid WithdrawMethodId) : IRequest<Result<WithdrawMethodDto>>;

public sealed class GetWithdrawMethodQueryHandler : IRequestHandler<GetWithdrawMethodQuery, Result<WithdrawMethodDto>>
{
    private readonly IWithdrawMethodRepository _repository;
    public GetWithdrawMethodQueryHandler(IWithdrawMethodRepository repository) => _repository = repository;

    public async Task<Result<WithdrawMethodDto>> Handle(GetWithdrawMethodQuery request, CancellationToken ct)
    {
        var method = await _repository.GetByIdAsync(request.WithdrawMethodId, ct);
        return method is null
            ? Result.Failure<WithdrawMethodDto>("Withdraw method not found.")
            : Result.Success(WithdrawMethodDto.FromEntity(method));
    }
}

// ── List ───────────────────────────────────────────────────────────────────

public sealed record ListWithdrawMethodsQuery(bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<WithdrawMethodDto>>>;

public sealed class ListWithdrawMethodsQueryHandler
    : IRequestHandler<ListWithdrawMethodsQuery, Result<IReadOnlyList<WithdrawMethodDto>>>
{
    private readonly IWithdrawMethodRepository _repository;
    public ListWithdrawMethodsQueryHandler(IWithdrawMethodRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<WithdrawMethodDto>>> Handle(ListWithdrawMethodsQuery request, CancellationToken ct)
    {
        var items = await _repository.ListAsync(request.IncludeInactive, ct);
        return Result.Success<IReadOnlyList<WithdrawMethodDto>>(items.Select(WithdrawMethodDto.FromEntity).ToList());
    }
}
