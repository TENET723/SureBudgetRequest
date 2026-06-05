using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Departments.Commands.UpsertDepartmentMonthlyBudget;

public sealed record MonthAmountEntry(int Month, decimal? Amount);

public sealed record UpsertDepartmentMonthlyBudgetCommand(
    Guid DepartmentId,
    int Year,
    List<MonthAmountEntry> Months,
    Guid ActorId) : IRequest<Result>;

public sealed class UpsertDepartmentMonthlyBudgetCommandHandler
    : IRequestHandler<UpsertDepartmentMonthlyBudgetCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IDepartmentMonthlyBudgetRepository _monthlyBudgetRepository;
    private readonly IAppSettingRepository _appSettingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpsertDepartmentMonthlyBudgetCommandHandler(
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository,
        IDepartmentMonthlyBudgetRepository monthlyBudgetRepository,
        IAppSettingRepository appSettingRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _monthlyBudgetRepository = monthlyBudgetRepository;
        _appSettingRepository = appSettingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpsertDepartmentMonthlyBudgetCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Verify actor has Finance role
        var actor = await _userRepository.GetByIdAsync(command.ActorId, cancellationToken);
        if (actor is null)
            return Result.Failure(UserErrors.NotFound(command.ActorId));
        if (actor.Role != UserRole.Finance)
            return Result.Failure(DepartmentErrors.Forbidden);

        // 2. Verify department exists
        var department = await _departmentRepository.GetByIdAsync(command.DepartmentId, cancellationToken);
        if (department is null)
            return Result.Failure(DepartmentErrors.NotFound);

        // 3. Read FinancialYear.StartMonth (default 4)
        var startMonthSetting = await _appSettingRepository.GetByKeyAsync("FinancialYear.StartMonth", cancellationToken);
        int fyStartMonth = startMonthSetting is not null && int.TryParse(startMonthSetting.Value, out var sm) ? sm : 4;

        var now = DateTime.UtcNow;

        // 4. Process each entry
        foreach (var entry in command.Months)
        {
            if (entry.Month < 1 || entry.Month > 12)
                continue;

            // Compute the calendar year for this FY month
            // e.g. FY starting April 2024: month 4 = Apr 2024, month 3 = Mar 2025
            int calendarYear = entry.Month >= fyStartMonth
                ? command.Year
                : command.Year + 1;

            // Month end = first day of next calendar month at 00:00 UTC
            var monthEnd = new DateTime(calendarYear, entry.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddMonths(1);

            // Skip silently if month already over
            if (monthEnd <= now)
                continue;

            // null = no preset, skip
            if (entry.Amount is null)
                continue;

            var existing = await _monthlyBudgetRepository.GetAsync(
                command.DepartmentId, command.Year, entry.Month, cancellationToken);

            if (existing is not null)
            {
                existing.Update(entry.Amount.Value, command.ActorId);
            }
            else
            {
                await _monthlyBudgetRepository.AddAsync(
                    new DepartmentMonthlyBudget(
                        command.DepartmentId,
                        command.Year,
                        entry.Month,
                        entry.Amount.Value,
                        command.ActorId),
                    cancellationToken);
            }
        }

        // 5. Save
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
