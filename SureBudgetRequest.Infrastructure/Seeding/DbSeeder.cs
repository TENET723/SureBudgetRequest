using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Infrastructure.Persistence;

namespace SureBudgetRequest.Infrastructure.Seeding;

/// <summary>
/// Seeds the database with initial data for development.
/// Each section checks its own table independently, so adding a new section
/// (e.g. currencies) will seed even on a database that already has users.
/// Run only when <c>ASPNETCORE_ENVIRONMENT</c> is Development.
/// </summary>
public sealed class DbSeeder
{
    private readonly AppDbContext _db;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(AppDbContext db, ILogger<DbSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedCurrenciesAsync(cancellationToken);
        await SeedUsersAndDepartmentsAsync(cancellationToken);
    }

    // ── Currencies ────────────────────────────────────────────────────────────
    // MMK MUST exist — it is the base currency referenced by every budget request
    // and the target of every limit comparison.
    private async Task SeedCurrenciesAsync(CancellationToken cancellationToken)
    {
        if (await _db.Currencies.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Currencies already seeded — skipping.");
            return;
        }

        _logger.LogInformation("Seeding currencies...");

        _db.Currencies.AddRange(
            new Currency("MMK", "Myanmar Kyat", 1m),
            new Currency("USD", "US Dollar", 4500m),
            new Currency("SGD", "Singapore Dollar", 3300m),
            new Currency("THB", "Thai Baht", 130m));

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ── Users / Departments ───────────────────────────────────────────────────
    private async Task SeedUsersAndDepartmentsAsync(CancellationToken cancellationToken)
    {
        if (await _db.Users.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Users already seeded — skipping.");
            return;
        }

        _logger.LogInformation("Seeding users and departments...");

        var adminId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var deptHeadItId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var deptHeadHrId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var bossId = Guid.Parse("00000000-0000-0000-0000-000000000004");
        var financeId = Guid.Parse("00000000-0000-0000-0000-000000000005");
        var employeeId = Guid.Parse("00000000-0000-0000-0000-000000000006");

        var itDeptId = Guid.Parse("00000000-0000-0000-0001-000000000001");
        var hrDeptId = Guid.Parse("00000000-0000-0000-0001-000000000002");
        var adminDeptId = Guid.Parse("00000000-0000-0000-0001-000000000003");

        var itDept = CreateDepartment(itDeptId, "Information Technology", deptHeadItId, 5_000_000);
        var hrDept = CreateDepartment(hrDeptId, "Human Resources", deptHeadHrId, 3_000_000);
        var adminDept = CreateDepartment(adminDeptId, "Administration", adminId, 10_000_000);

        _db.Departments.AddRange(itDept, hrDept, adminDept);

        var admin = CreateUser(adminId, "admin", "Mg Mg (Admin)", adminDeptId, UserRole.Admin);
        var deptHeadIt = CreateUser(deptHeadItId, "ko_zin", "Ko Zin Htet (IT Head)", itDeptId, UserRole.DepartmentHead);
        var deptHeadHr = CreateUser(deptHeadHrId, "ma_thida", "Ma Thida (HR Head)", hrDeptId, UserRole.DepartmentHead);
        var boss = CreateUser(bossId, "u_kyaw", "U Kyaw Zin (Boss)", adminDeptId, UserRole.Boss);
        var finance = CreateUser(financeId, "ko_aung", "Ko Aung Naing (Finance)", adminDeptId, UserRole.Finance);
        var employee = CreateUser(employeeId, "ma_aye", "Ma Aye Aye (Employee)", itDeptId, UserRole.Employee);

        _db.Users.AddRange(admin, deptHeadIt, deptHeadHr, boss, finance, employee);

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {DeptCount} departments and {UserCount} users.", 3, 6);
    }

    // ── Helpers (work around private setters via domain constructors) ─────────

    private static Department CreateDepartment(Guid id, string name, Guid headUserId, decimal limit)
        => new Department(name, headUserId, limit);

    private static User CreateUser(Guid id, string username, string fullName, Guid departmentId, UserRole role)
        => new User(username, fullName, departmentId, role);
}