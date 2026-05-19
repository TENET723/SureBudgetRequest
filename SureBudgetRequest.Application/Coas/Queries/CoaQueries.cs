using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Coas.Queries;

public sealed record CoaDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt)
{
    public static CoaDto FromEntity(Coa c) =>
        new(c.Id, c.Code, c.Name, c.Description, c.IsActive, c.CreatedAt);
}

// ── Get single ─────────────────────────────────────────────────────────────

public sealed record GetCoaQuery(Guid CoaId) : IRequest<Result<CoaDto>>;

public sealed class GetCoaQueryHandler : IRequestHandler<GetCoaQuery, Result<CoaDto>>
{
    private readonly ICoaRepository _repository;
    public GetCoaQueryHandler(ICoaRepository repository) => _repository = repository;

    public async Task<Result<CoaDto>> Handle(GetCoaQuery request, CancellationToken ct)
    {
        var coa = await _repository.GetByIdAsync(request.CoaId, ct);
        return coa is null
            ? Result.Failure<CoaDto>("Chart of Account not found.")
            : Result.Success(CoaDto.FromEntity(coa));
    }
}

// ── List ───────────────────────────────────────────────────────────────────

public sealed record ListCoasQuery(bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<CoaDto>>>;

public sealed class ListCoasQueryHandler
    : IRequestHandler<ListCoasQuery, Result<IReadOnlyList<CoaDto>>>
{
    private readonly ICoaRepository _repository;
    public ListCoasQueryHandler(ICoaRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<CoaDto>>> Handle(ListCoasQuery request, CancellationToken ct)
    {
        var items = await _repository.ListAsync(request.IncludeInactive, ct);
        return Result.Success<IReadOnlyList<CoaDto>>(items.Select(CoaDto.FromEntity).ToList());
    }
}
