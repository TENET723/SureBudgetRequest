using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Departments.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record MonthPlanDto(
    int Month,
    decimal? PresetAmount,
    decimal EffectiveLimit,
    decimal ActualSpentInMmk,
    bool IsEditable,
    bool IsCurrent);

public sealed record DepartmentBudgetPlanDto(
    Guid DepartmentId,
    string DepartmentName,
    int Year,
    decimal FallbackMonthlyLimit,
    IReadOnlyList<MonthPlanDto> Months);

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetDepartmentBudgetPlanQuery(Guid DepartmentId, int Year)
    : IRequest<Result<DepartmentBudgetPlanDto>>;

public sealed class GetDepartmentBudgetPlanQueryHandler
    : IRequestHandler<GetDepartmentBudgetPlanQuery, Result<DepartmentBudgetPlanDto>>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IDepartmentMonthlyBudgetRepository _monthlyBudgetRepository;
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IAppSettingRepository _appSettingRepository;
    private readonly IDateTimeProvider _clock;

    public GetDepartmentBudgetPlanQueryHandler(
        IDepartmentRepository departmentRepository,
        IDepartmentMonthlyBudgetRepository monthlyBudgetRepository,
        IBudgetRequestRepository budgetRequestRepository,
        IAppSettingRepository appSettingRepository,
        IDateTimeProvider clock)
    {
        _departmentRepository = departmentRepository;
        _monthlyBudgetRepository = monthlyBudgetRepository;
        _budgetRequestRepository = budgetRequestRepository;
        _appSettingRepository = appSettingRepository;
        _clock = clock;
    }

    public async Task<Result<DepartmentBudgetPlanDto>> Handle(
        GetDepartmentBudgetPlanQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Load department
        var department = await _departmentRepository.GetByIdAsync(request.DepartmentId, cancellationToken);
        if (department is null)
            return Result.Failure<DepartmentBudgetPlanDto>(DepartmentErrors.NotFound);

        // 2. Read FY start month
        var startMonthSetting = await _appSettingRepository.GetByKeyAsync("FinancialYear.StartMonth", cancellationToken);
        int fyStartMonth = startMonthSetting is not null && int.TryParse(startMonthSetting.Value, out var sm) ? sm : 4;

        // 3. Load all presets for this department/year
        var presets = await _monthlyBudgetRepository.ListByDepartmentYearAsync(
            request.DepartmentId, request.Year, cancellationToken);

        var utcNow = _clock.UtcNow;
        var months = new List<MonthPlanDto>(12);

        // 4. Iterate through 12 FY months starting from fyStartMonth
        for (int i = 0; i < 12; i++)
        {
            int calMonth = ((fyStartMonth - 1 + i) % 12) + 1;
            int calYear = calMonth >= fyStartMonth ? request.Year : request.Year + 1;

            var monthStartUtc = new DateTime(calYear, calMonth, 1, 0, 0, 0, DateTimeKind.Utc)
                - _clock.BusinessUtcOffset;
            var monthEndUtc = new DateTime(calYear, calMonth, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddMonths(1) - _clock.BusinessUtcOffset;

            // a. Find matching preset
            var preset = presets.FirstOrDefault(p => p.Month == calMonth);
            decimal? presetAmount = preset?.Amount;

            // b. Effective limit
            decimal effectiveLimit = presetAmount ?? department.MonthlyLimit;

            // c. Actual spend
            var actualSpent = await _budgetRequestRepository.GetApprovedSpendInMmkAsync(
                department.Id, monthStartUtc, monthEndUtc, cancellationToken);

            // d. Flags
            bool isEditable = monthEndUtc > utcNow;
            bool isCurrent = utcNow >= monthStartUtc && utcNow < monthEndUtc;

            months.Add(new MonthPlanDto(
                Month: calMonth,
                PresetAmount: presetAmount,
                EffectiveLimit: effectiveLimit,
                ActualSpentInMmk: actualSpent,
                IsEditable: isEditable,
                IsCurrent: isCurrent));
        }

        return Result.Success(new DepartmentBudgetPlanDto(
            department.Id,
            department.Name,
            request.Year,
            department.MonthlyLimit,
            months));
    }
}
