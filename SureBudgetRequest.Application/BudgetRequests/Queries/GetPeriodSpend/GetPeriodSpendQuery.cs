using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
// Domain.Common is used for the Result type returned by the handlers.

namespace SureBudgetRequest.Application.BudgetRequests.Queries.GetPeriodSpend;

/// <summary>
/// DTO returned by <see cref="GetPeriodSpendQuery"/>. All amounts are in MMK.
///
/// When the department has no cumulative cap configured for the current financial
/// year, <see cref="PeriodType"/> is <see cref="BudgetPeriodType.None"/>,
/// <see cref="PeriodLimit"/> and <see cref="RemainingInMmk"/> are <c>null</c>, and
/// the window/spend fields are zero/default — the UI should NOT show any
/// overrun warning or justification field.
/// </summary>
public sealed record PeriodSpendDto(
    Guid DepartmentId,
    BudgetPeriodType PeriodType,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    decimal SpentInMmk,
    decimal? PeriodLimit,
    decimal? RemainingInMmk);

/// <summary>
/// Returns the department's already-approved spend for the active period window
/// (per its current-FY cadence), alongside the configured limit.
///
/// The form uses this to show "X / Y MMK used this {period}" and to decide
/// whether to require a justification when the entered amount would push the
/// department over the cap.
/// </summary>
public sealed record GetPeriodSpendQuery(Guid DepartmentId)
    : IRequest<Result<PeriodSpendDto>>;

public sealed class GetPeriodSpendQueryHandler
    : IRequestHandler<GetPeriodSpendQuery, Result<PeriodSpendDto>>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IDepartmentBudgetPeriodRepository _periodRepository;
    private readonly IFinancialYearProvider _financialYearProvider;

    public GetPeriodSpendQueryHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IDepartmentRepository departmentRepository,
        IDepartmentBudgetPeriodRepository periodRepository,
        IFinancialYearProvider financialYearProvider)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _departmentRepository = departmentRepository;
        _periodRepository = periodRepository;
        _financialYearProvider = financialYearProvider;
    }

    public async Task<Result<PeriodSpendDto>> Handle(
        GetPeriodSpendQuery request,
        CancellationToken cancellationToken)
    {
        var department = await _departmentRepository.GetByIdAsync(request.DepartmentId, cancellationToken);
        if (department is null)
            return Result.Failure<PeriodSpendDto>(BudgetRequestErrors.DepartmentNotFound);

        var currentFy = await _financialYearProvider.GetCurrentFinancialYearAsync(cancellationToken);
        var active = await _periodRepository.GetActiveAsync(department.Id, currentFy, cancellationToken);
        var periodType = active?.PeriodType ?? BudgetPeriodType.None;

        if (periodType == BudgetPeriodType.None)
        {
            // No cumulative cap — return a "None" DTO; the UI shows no usage line.
            return Result.Success(new PeriodSpendDto(
                department.Id, BudgetPeriodType.None,
                default, default, 0m, null, null));
        }

        var (startUtc, endUtc) = await _financialYearProvider.GetPeriodWindowUtcAsync(periodType, cancellationToken);
        var spent = await _budgetRequestRepository.GetApprovedSpendInMmkAsync(
            department.Id, startUtc, endUtc, cancellationToken);

        var limit = active!.LimitAmount;
        return Result.Success(new PeriodSpendDto(
            department.Id, periodType, startUtc, endUtc, spent, limit, limit - spent));
    }
}
