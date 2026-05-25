using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.GetMonthlySpend;

/// <summary>
/// DTO returned by <see cref="GetMonthlySpendQuery"/>. All amounts are in MMK.
///
/// When the department has no monthly limit configured, <see cref="MonthlyLimit"/>
/// and <see cref="RemainingInMmk"/> are <c>null</c> and the UI should NOT show
/// any monthly-overrun warning or justification field. <see cref="SpentInMmk"/>
/// is still populated (for display) regardless of whether enforcement is on.
/// </summary>
public sealed record MonthlySpendDto(
    Guid DepartmentId,
    int Year,
    int Month,
    decimal SpentInMmk,
    decimal? MonthlyLimit,
    decimal? RemainingInMmk);

/// <summary>
/// Returns the department's already-approved spend for the current UTC
/// calendar month, alongside the department's configured monthly limit.
///
/// The form uses this to show "X / Y MMK used this month" and to decide
/// whether to require a justification when the user enters an amount that
/// would push the department over the cap.
/// </summary>
public sealed record GetMonthlySpendQuery(Guid DepartmentId)
    : IRequest<Result<MonthlySpendDto>>;

public sealed class GetMonthlySpendQueryHandler
    : IRequestHandler<GetMonthlySpendQuery, Result<MonthlySpendDto>>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IDepartmentRepository _departmentRepository;

    public GetMonthlySpendQueryHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IDepartmentRepository departmentRepository)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _departmentRepository = departmentRepository;
    }

    public async Task<Result<MonthlySpendDto>> Handle(
        GetMonthlySpendQuery request,
        CancellationToken cancellationToken)
    {
        var department = await _departmentRepository.GetByIdAsync(
            request.DepartmentId, cancellationToken);
        if (department is null)
            return Result.Failure<MonthlySpendDto>(BudgetRequestErrors.DepartmentNotFound);

        var nowUtc = DateTime.UtcNow;
        var spent = await _budgetRequestRepository.GetMonthlyApprovedSpendInMmkAsync(
            department.Id, nowUtc.Year, nowUtc.Month, cancellationToken);

        decimal? remaining = department.MonthlyLimit.HasValue
            ? department.MonthlyLimit.Value - spent
            : null;

        return Result.Success(new MonthlySpendDto(
            department.Id, nowUtc.Year, nowUtc.Month,
            spent, department.MonthlyLimit, remaining));
    }
}
