using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Departments.Queries;

public sealed record DepartmentDto(
    Guid Id,
    string Name,
    Guid? HeadUserId,
    decimal BudgetLimit,
    string? SlackWebhookUrl,
    bool IsActive,
    DateTime CreatedAt,
    // ── Cumulative-cap config (effective-dated) ──────────────────────────────
    int CurrentFinancialYear,
    // Config in force for the current FY. PeriodType = None when uncapped.
    BudgetPeriodType CurrentPeriodType,
    decimal CurrentPeriodLimit,
    // Pending config that takes effect from next FY, when one has been scheduled.
    int? PendingFinancialYear,
    BudgetPeriodType? PendingPeriodType,
    decimal? PendingPeriodLimit)
{
    /// <summary>
    /// Builds the DTO from the department plus its resolved config rows.
    /// <paramref name="active"/> is the row in force for <paramref name="currentFinancialYear"/>;
    /// <paramref name="pending"/> is the row (if any) effective from the next FY.
    /// </summary>
    public static DepartmentDto FromEntity(
        Department d,
        int currentFinancialYear,
        DepartmentBudgetPeriod? active,
        DepartmentBudgetPeriod? pending) =>
        new(d.Id, d.Name, d.HeadUserId, d.BudgetLimit, d.SlackWebhookUrl, d.IsActive, d.CreatedAt,
            currentFinancialYear,
            active?.PeriodType ?? BudgetPeriodType.None,
            active?.LimitAmount ?? 0m,
            pending?.EffectiveFromFinancialYear,
            pending?.PeriodType,
            pending?.LimitAmount);
}

// ── Get single department ─────────────────────────────────────────────────────

public sealed record GetDepartmentQuery(Guid DepartmentId) : IRequest<Result<DepartmentDto>>;

public sealed class GetDepartmentQueryHandler : IRequestHandler<GetDepartmentQuery, Result<DepartmentDto>>
{
    private readonly IDepartmentRepository _repository;
    private readonly IDepartmentBudgetPeriodRepository _periodRepository;
    private readonly IFinancialYearProvider _financialYearProvider;

    public GetDepartmentQueryHandler(
        IDepartmentRepository repository,
        IDepartmentBudgetPeriodRepository periodRepository,
        IFinancialYearProvider financialYearProvider)
    {
        _repository = repository;
        _periodRepository = periodRepository;
        _financialYearProvider = financialYearProvider;
    }

    public async Task<Result<DepartmentDto>> Handle(GetDepartmentQuery request, CancellationToken ct)
    {
        var dept = await _repository.GetByIdAsync(request.DepartmentId, ct);
        if (dept is null) return Result.Failure<DepartmentDto>(DepartmentErrors.NotFound);

        var currentFy = await _financialYearProvider.GetCurrentFinancialYearAsync(ct);
        var periods = await _periodRepository.ListByDepartmentAsync(dept.Id, ct);
        var (active, pending) = ResolveConfig(periods, currentFy);

        return Result.Success(DepartmentDto.FromEntity(dept, currentFy, active, pending));
    }

    internal static (DepartmentBudgetPeriod? Active, DepartmentBudgetPeriod? Pending) ResolveConfig(
        IReadOnlyList<DepartmentBudgetPeriod> periods, int currentFy)
    {
        var active = periods
            .Where(p => p.EffectiveFromFinancialYear <= currentFy)
            .OrderByDescending(p => p.EffectiveFromFinancialYear)
            .FirstOrDefault();
        var pending = periods.FirstOrDefault(p => p.EffectiveFromFinancialYear == currentFy + 1);
        return (active, pending);
    }
}

// ── List departments ──────────────────────────────────────────────────────────

public sealed record ListDepartmentsQuery(bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<DepartmentDto>>>;

public sealed class ListDepartmentsQueryHandler
    : IRequestHandler<ListDepartmentsQuery, Result<IReadOnlyList<DepartmentDto>>>
{
    private readonly IDepartmentRepository _repository;
    private readonly IDepartmentBudgetPeriodRepository _periodRepository;
    private readonly IFinancialYearProvider _financialYearProvider;

    public ListDepartmentsQueryHandler(
        IDepartmentRepository repository,
        IDepartmentBudgetPeriodRepository periodRepository,
        IFinancialYearProvider financialYearProvider)
    {
        _repository = repository;
        _periodRepository = periodRepository;
        _financialYearProvider = financialYearProvider;
    }

    public async Task<Result<IReadOnlyList<DepartmentDto>>> Handle(ListDepartmentsQuery request, CancellationToken ct)
    {
        var depts = await _repository.ListAsync(request.IncludeInactive, ct);
        var currentFy = await _financialYearProvider.GetCurrentFinancialYearAsync(ct);

        var dtos = new List<DepartmentDto>(depts.Count);
        foreach (var dept in depts)
        {
            var periods = await _periodRepository.ListByDepartmentAsync(dept.Id, ct);
            var (active, pending) = GetDepartmentQueryHandler.ResolveConfig(periods, currentFy);
            dtos.Add(DepartmentDto.FromEntity(dept, currentFy, active, pending));
        }

        return Result.Success<IReadOnlyList<DepartmentDto>>(dtos);
    }
}
