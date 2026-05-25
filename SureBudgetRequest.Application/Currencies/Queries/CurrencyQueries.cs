using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Currencies.Queries;

public sealed record CurrencyDto(
    string Code,
    string Name,
    decimal RateToMmk,
    bool IsActive,
    DateTime RateUpdatedAt,
    DateTime CreatedAt)
{
    public static CurrencyDto FromEntity(Currency c) =>
        new(c.Code, c.Name, c.RateToMmk, c.IsActive, c.RateUpdatedAt, c.CreatedAt);
}

public sealed record CurrencyRateChangeDto(
    Guid Id,
    string CurrencyCode,
    decimal OldRate,
    decimal NewRate,
    Guid ChangedByUserId,
    DateTime ChangedAt)
{
    public static CurrencyRateChangeDto FromEntity(CurrencyRateChange c) =>
        new(c.Id, c.CurrencyCode, c.OldRate, c.NewRate, c.ChangedByUserId, c.ChangedAt);
}

// ── Get single currency ───────────────────────────────────────────────────────

public sealed record GetCurrencyQuery(string Code) : IRequest<Result<CurrencyDto>>;

public sealed class GetCurrencyQueryHandler : IRequestHandler<GetCurrencyQuery, Result<CurrencyDto>>
{
    private readonly ICurrencyRepository _repository;
    public GetCurrencyQueryHandler(ICurrencyRepository repository) => _repository = repository;

    public async Task<Result<CurrencyDto>> Handle(GetCurrencyQuery request, CancellationToken ct)
    {
        var c = await _repository.GetByCodeAsync(request.Code, ct);
        return c is null
            ? Result.Failure<CurrencyDto>(CurrencyErrors.GenericNotFound)
            : Result.Success(CurrencyDto.FromEntity(c));
    }
}

// ── List currencies ───────────────────────────────────────────────────────────

public sealed record ListCurrenciesQuery(bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<CurrencyDto>>>;

public sealed class ListCurrenciesQueryHandler
    : IRequestHandler<ListCurrenciesQuery, Result<IReadOnlyList<CurrencyDto>>>
{
    private readonly ICurrencyRepository _repository;
    public ListCurrenciesQueryHandler(ICurrencyRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<CurrencyDto>>> Handle(ListCurrenciesQuery request, CancellationToken ct)
    {
        var items = await _repository.ListAsync(request.IncludeInactive, ct);
        return Result.Success<IReadOnlyList<CurrencyDto>>(
            items.Select(CurrencyDto.FromEntity).ToList());
    }
}

// ── List rate change history ──────────────────────────────────────────────────

public sealed record ListCurrencyRateChangesQuery(string? CurrencyCode = null, int? Take = 50)
    : IRequest<Result<IReadOnlyList<CurrencyRateChangeDto>>>;

public sealed class ListCurrencyRateChangesQueryHandler
    : IRequestHandler<ListCurrencyRateChangesQuery, Result<IReadOnlyList<CurrencyRateChangeDto>>>
{
    private readonly ICurrencyRepository _repository;
    public ListCurrencyRateChangesQueryHandler(ICurrencyRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<CurrencyRateChangeDto>>> Handle(
        ListCurrencyRateChangesQuery request, CancellationToken ct)
    {
        var items = await _repository.ListRateChangesAsync(request.CurrencyCode, request.Take, ct);
        return Result.Success<IReadOnlyList<CurrencyRateChangeDto>>(
            items.Select(CurrencyRateChangeDto.FromEntity).ToList());
    }
}
