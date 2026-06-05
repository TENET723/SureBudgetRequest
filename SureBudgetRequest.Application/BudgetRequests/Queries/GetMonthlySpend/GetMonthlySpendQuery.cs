using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.GetMonthlySpend;

/// <summary>
/// DTO returned by <see cref="GetMonthlySpendQuery"/>. All amounts are in MMK.
/// MonthlyLimit is the department's fallback cap; EffectiveLimit is the resolved
/// value (preset for this month if one exists, otherwise MonthlyLimit).
/// </summary>
public sealed record MonthlySpendDto(
    Guid DepartmentId,
    DateTime MonthStartUtc,
    DateTime MonthEndUtc,
    decimal SpentInMmk,
    decimal MonthlyLimit,
    decimal EffectiveLimit,
    decimal RemainingInMmk);

/// <summary>
/// Returns the department's already-approved spend for the current calendar month,
/// alongside the effective monthly limit (preset → fallback).
/// </summary>
public sealed record GetMonthlySpendQuery(Guid DepartmentId)
    : IRequest<Result<MonthlySpendDto>>;

public sealed class GetMonthlySpendQueryHandler
    : IRequestHandler<GetMonthlySpendQuery, Result<MonthlySpendDto>>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IDepartmentMonthlyBudgetRepository _monthlyBudgetRepository;
    private readonly IAppSettingRepository _appSettingRepository;
    private readonly IDateTimeProvider _clock;

    public GetMonthlySpendQueryHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IDepartmentRepository departmentRepository,
        IDepartmentMonthlyBudgetRepository monthlyBudgetRepository,
        IAppSettingRepository appSettingRepository,
        IDateTimeProvider clock)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _departmentRepository = departmentRepository;
        _monthlyBudgetRepository = monthlyBudgetRepository;
        _appSettingRepository = appSettingRepository;
        _clock = clock;
    }

    public async Task<Result<MonthlySpendDto>> Handle(
        GetMonthlySpendQuery request,
        CancellationToken cancellationToken)
    {
        var department = await _departmentRepository.GetByIdAsync(request.DepartmentId, cancellationToken);
        if (department is null)
            return Result.Failure<MonthlySpendDto>(BudgetRequestErrors.DepartmentNotFound);

        var businessNow = _clock.BusinessNow;
        var (startUtc, endUtc) = MonthlyBudgetWindow.CurrentUtc(businessNow, _clock.BusinessUtcOffset);

        var spent = await _budgetRequestRepository.GetApprovedSpendInMmkAsync(
            department.Id, startUtc, endUtc, cancellationToken);

        // Resolve effective limit: preset for this FY month, else fallback
        var startMonthSetting = await _appSettingRepository.GetByKeyAsync("FinancialYear.StartMonth", cancellationToken);
        int fyStartMonth = startMonthSetting is not null && int.TryParse(startMonthSetting.Value, out var sm) ? sm : 4;
        int currentFy = businessNow.Month >= fyStartMonth ? businessNow.Year : businessNow.Year - 1;

        var preset = await _monthlyBudgetRepository.GetAsync(
            department.Id, currentFy, businessNow.Month, cancellationToken);
        var effectiveLimit = preset?.Amount ?? department.MonthlyLimit;

        return Result.Success(new MonthlySpendDto(
            department.Id, startUtc, endUtc, spent,
            department.MonthlyLimit, effectiveLimit, effectiveLimit - spent));
    }
}
