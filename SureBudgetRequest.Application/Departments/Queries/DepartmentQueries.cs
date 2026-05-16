using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Departments.Queries;

public sealed record DepartmentDto(
    Guid Id,
    string Name,
    Guid? HeadUserId,
    decimal BudgetLimit,
    string? SlackWebhookUrl,
    bool IsActive,
    DateTime CreatedAt)
{
    public static DepartmentDto FromEntity(Department d) =>
        new(d.Id, d.Name, d.HeadUserId, d.BudgetLimit, d.SlackWebhookUrl, d.IsActive, d.CreatedAt);
}

// ── Get single department ─────────────────────────────────────────────────────

public sealed record GetDepartmentQuery(Guid DepartmentId) : IRequest<Result<DepartmentDto>>;

public sealed class GetDepartmentQueryHandler : IRequestHandler<GetDepartmentQuery, Result<DepartmentDto>>
{
    private readonly IDepartmentRepository _repository;
    public GetDepartmentQueryHandler(IDepartmentRepository repository) => _repository = repository;

    public async Task<Result<DepartmentDto>> Handle(GetDepartmentQuery request, CancellationToken ct)
    {
        var dept = await _repository.GetByIdAsync(request.DepartmentId, ct);
        return dept is null
            ? Result.Failure<DepartmentDto>("Department not found.")
            : Result.Success(DepartmentDto.FromEntity(dept));
    }
}

// ── List departments ──────────────────────────────────────────────────────────

public sealed record ListDepartmentsQuery(bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<DepartmentDto>>>;

public sealed class ListDepartmentsQueryHandler
    : IRequestHandler<ListDepartmentsQuery, Result<IReadOnlyList<DepartmentDto>>>
{
    private readonly IDepartmentRepository _repository;
    public ListDepartmentsQueryHandler(IDepartmentRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<DepartmentDto>>> Handle(ListDepartmentsQuery request, CancellationToken ct)
    {
        var depts = await _repository.ListAsync(request.IncludeInactive, ct);
        return Result.Success<IReadOnlyList<DepartmentDto>>(depts.Select(DepartmentDto.FromEntity).ToList());
    }
}
