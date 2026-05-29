using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.BudgetCategories.Queries;

public sealed record BudgetCategoryDto(
    Guid Id,
    string Name,
    bool IsActive,
    DateTime CreatedAt)
{
    public static BudgetCategoryDto FromEntity(BudgetCategory c) =>
        new(c.Id, c.Name, c.IsActive, c.CreatedAt);
}

// ── Get single ─────────────────────────────────────────────────────────────

public sealed record GetBudgetCategoryQuery(Guid BudgetCategoryId) : IRequest<Result<BudgetCategoryDto>>;

public sealed class GetBudgetCategoryQueryHandler : IRequestHandler<GetBudgetCategoryQuery, Result<BudgetCategoryDto>>
{
    private readonly IBudgetCategoryRepository _repository;
    public GetBudgetCategoryQueryHandler(IBudgetCategoryRepository repository) => _repository = repository;

    public async Task<Result<BudgetCategoryDto>> Handle(GetBudgetCategoryQuery request, CancellationToken ct)
    {
        var category = await _repository.GetByIdAsync(request.BudgetCategoryId, ct);
        return category is null
            ? Result.Failure<BudgetCategoryDto>("Budget category not found.")
            : Result.Success(BudgetCategoryDto.FromEntity(category));
    }
}

// ── List ───────────────────────────────────────────────────────────────────

public sealed record ListBudgetCategoriesQuery(bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<BudgetCategoryDto>>>;

public sealed class ListBudgetCategoriesQueryHandler
    : IRequestHandler<ListBudgetCategoriesQuery, Result<IReadOnlyList<BudgetCategoryDto>>>
{
    private readonly IBudgetCategoryRepository _repository;
    public ListBudgetCategoriesQueryHandler(IBudgetCategoryRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<BudgetCategoryDto>>> Handle(ListBudgetCategoriesQuery request, CancellationToken ct)
    {
        var items = await _repository.ListAsync(request.IncludeInactive, ct);
        return Result.Success<IReadOnlyList<BudgetCategoryDto>>(items.Select(BudgetCategoryDto.FromEntity).ToList());
    }
}
