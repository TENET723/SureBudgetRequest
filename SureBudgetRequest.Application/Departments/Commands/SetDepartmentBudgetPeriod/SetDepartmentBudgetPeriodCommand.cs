using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Departments.Commands.SetDepartmentBudgetPeriod;

/// <summary>
/// Sets (or replaces) a department's cumulative-cap configuration for the NEXT
/// financial year. Cadence and amount changes always defer to FY{current+1} —
/// the active config for the current year is never mutated. A brand-new
/// department's first (immediate) config is written by CreateDepartment instead.
/// </summary>
public sealed record SetDepartmentBudgetPeriodCommand(
    Guid DepartmentId,
    BudgetPeriodType PeriodType,
    decimal LimitAmount) : IRequest<Result>;

public sealed class SetDepartmentBudgetPeriodCommandHandler
    : IRequestHandler<SetDepartmentBudgetPeriodCommand, Result>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IDepartmentBudgetPeriodRepository _periodRepository;
    private readonly IFinancialYearProvider _financialYearProvider;
    private readonly IUnitOfWork _unitOfWork;

    public SetDepartmentBudgetPeriodCommandHandler(
        IDepartmentRepository departmentRepository,
        IDepartmentBudgetPeriodRepository periodRepository,
        IFinancialYearProvider financialYearProvider,
        IUnitOfWork unitOfWork)
    {
        _departmentRepository = departmentRepository;
        _periodRepository = periodRepository;
        _financialYearProvider = financialYearProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(SetDepartmentBudgetPeriodCommand command, CancellationToken ct)
    {
        var dept = await _departmentRepository.GetByIdAsync(command.DepartmentId, ct);
        if (dept is null) return Result.Failure(DepartmentErrors.NotFound);

        var currentFy = await _financialYearProvider.GetCurrentFinancialYearAsync(ct);
        var nextFy = currentFy + 1;

        DepartmentBudgetPeriod period;
        try
        {
            period = new DepartmentBudgetPeriod(dept.Id, nextFy, command.PeriodType, command.LimitAmount);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(DepartmentErrors.ValidationError(ex.Message));
        }

        await _periodRepository.UpsertAsync(period, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
