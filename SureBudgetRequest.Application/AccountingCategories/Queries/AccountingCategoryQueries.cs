using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.AccountingCategories.Queries;

public sealed record AccountingCategoryDto(
    Guid Id,
    string Name,
    bool IsActive,
    DateTime CreatedAt)
{
    public static AccountingCategoryDto FromEntity(AccountingCategory c) =>
        new(c.Id, c.Name, c.IsActive, c.CreatedAt);
}

// ── Get single ─────────────────────────────────────────────────────────────

public sealed record GetAccountingCategoryQuery(Guid AccountingCategoryId) : IRequest<Result<AccountingCategoryDto>>;

public sealed class GetAccountingCategoryQueryHandler : IRequestHandler<GetAccountingCategoryQuery, Result<AccountingCategoryDto>>
{
    private readonly IAccountingCategoryRepository _repository;
    public GetAccountingCategoryQueryHandler(IAccountingCategoryRepository repository) => _repository = repository;

    public async Task<Result<AccountingCategoryDto>> Handle(GetAccountingCategoryQuery request, CancellationToken ct)
    {
        var category = await _repository.GetByIdAsync(request.AccountingCategoryId, ct);
        return category is null
            ? Result.Failure<AccountingCategoryDto>(AccountingCategoryErrors.GenericNotFound)
            : Result.Success(AccountingCategoryDto.FromEntity(category));
    }
}

// ── List ───────────────────────────────────────────────────────────────────

public sealed record ListAccountingCategoriesQuery(bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<AccountingCategoryDto>>>;

public sealed class ListAccountingCategoriesQueryHandler
    : IRequestHandler<ListAccountingCategoriesQuery, Result<IReadOnlyList<AccountingCategoryDto>>>
{
    private readonly IAccountingCategoryRepository _repository;
    public ListAccountingCategoriesQueryHandler(IAccountingCategoryRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<AccountingCategoryDto>>> Handle(ListAccountingCategoriesQuery request, CancellationToken ct)
    {
        var items = await _repository.ListAsync(request.IncludeInactive, ct);
        return Result.Success<IReadOnlyList<AccountingCategoryDto>>(items.Select(AccountingCategoryDto.FromEntity).ToList());
    }
}
