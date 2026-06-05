using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;
// Domain.Common is used for the Result type returned by the handlers.

namespace SureBudgetRequest.Application.BudgetRequests.Queries.GetMonthlySpend;

/// <summary>
/// DTO returned by <see cref="GetMonthlySpendQuery"/>. All amounts are in MMK.
/// Every department has a monthly limit, so the limit and remaining figures are
/// always populated. The window is the current calendar-month interval in UTC.
/// </summary>
public sealed record MonthlySpendDto(
    Guid DepartmentId,
    DateTime MonthStartUtc,
    DateTime MonthEndUtc,
    decimal SpentInMmk,
    decimal MonthlyLimit,
    decimal RemainingInMmk);

/// <summary>
/// Returns the department's already-approved spend for the current calendar month,
/// alongside the configured monthly limit.
///
/// The form uses this to show "X / Y MMK used this month" and to decide whether to
/// require a justification when the entered amount would push the department over
/// the cap.
/// </summary>
public sealed record GetMonthlySpendQuery(Guid DepartmentId)
    : IRequest<Result<MonthlySpendDto>>;

public sealed class GetMonthlySpendQueryHandler
    : IRequestHandler<GetMonthlySpendQuery, Result<MonthlySpendDto>>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IDateTimeProvider _clock;

    public GetMonthlySpendQueryHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IDepartmentRepository departmentRepository,
        IDateTimeProvider clock)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _departmentRepository = departmentRepository;
        _clock = clock;
    }

    public async Task<Result<MonthlySpendDto>> Handle(
        GetMonthlySpendQuery request,
        CancellationToken cancellationToken)
    {
        var department = await _departmentRepository.GetByIdAsync(request.DepartmentId, cancellationToken);
        if (department is null)
            return Result.Failure<MonthlySpendDto>(BudgetRequestErrors.DepartmentNotFound);

        var (startUtc, endUtc) = MonthlyBudgetWindow.CurrentUtc(
            _clock.BusinessNow, _clock.BusinessUtcOffset);

        var spent = await _budgetRequestRepository.GetApprovedSpendInMmkAsync(
            department.Id, startUtc, endUtc, cancellationToken);

        var limit = department.MonthlyLimit;
        return Result.Success(new MonthlySpendDto(
            department.Id, startUtc, endUtc, spent, limit, limit - spent));
    }
}
