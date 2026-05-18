using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SureBudgetRequest.Application.Abstractions.Security;
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
    /// <summary>
    /// Default password for all seeded users. All seeded users have
    /// <c>MustChangePassword = true</c>, so they will be forced to change
    /// this on their first login.
    /// </summary>
    public const string SeedPassword = "Welcome123!";

    private readonly AppDbContext _db;
    private readonly ILogger<DbSeeder> _logger;
    private readonly IPasswordHasher _passwordHasher;

    public DbSeeder(AppDbContext db, ILogger<DbSeeder> logger, IPasswordHasher passwordHasher)
    {
        _db = db;
        _logger = logger;
        _passwordHasher = passwordHasher;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedCurrenciesAsync(cancellationToken);
        await SeedUsersAndDepartmentsAsync(cancellationToken);
    }

    // ── Currencies ────────────────────────────────────────────────────────────
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

        _logger.LogInformation("Seeding users and departments (initial password: {Password})...", SeedPassword);

        var adminId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var deptHeadItId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var deptHeadHrId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var bossId = Guid.Parse("00000000-0000-0000-0000-000000000004");
        var financeId = Guid.Parse("00000000-0000-0000-0000-000000000005");
        var employeeId = Guid.Parse("00000000-0000-0000-0000-000000000006");

        var itDeptId = Guid.Parse("00000000-0000-0000-0001-000000000001");
        var hrDeptId = Guid.Parse("00000000-0000-0000-0001-000000000002");
        var adminDeptId = Guid.Parse("00000000-0000-0000-0001-000000000003");

        // Per-request limit (BudgetLimit) is the threshold for Management routing.
        // MonthlyLimit is the soft cap that triggers a required justification at
        // submission. We seed sample values; admins can adjust per-department later.
        // Pass null for monthlyLimit to disable monthly enforcement on a department.
        var itDept = new Department("Information Technology", deptHeadItId,
            budgetLimit: 5_000_000, monthlyLimit: 20_000_000);
        var hrDept = new Department("Human Resources", deptHeadHrId,
            budgetLimit: 3_000_000, monthlyLimit: 10_000_000);
        var adminDept = new Department("Administration", adminId,
            budgetLimit: 10_000_000, monthlyLimit: 40_000_000);

        _db.Departments.AddRange(itDept, hrDept, adminDept);

        var admin = CreateUser("admin", "Mg Mg (Admin)", "admin@asure.local", adminDeptId, UserRole.Admin);
        var deptHeadIt = CreateUser("ko_zin", "Ko Zin Htet (IT Head)", "ko.zin@asure.local", itDeptId, UserRole.DepartmentHead);
        var deptHeadHr = CreateUser("ma_thida", "Ma Thida (HR Head)", "ma.thida@asure.local", hrDeptId, UserRole.DepartmentHead);
        var mgmt1 = CreateUser("u_kyaw", "U Kyaw Zin (Management)", "u.kyaw@asure.local", adminDeptId, UserRole.Management);
        var mgmt2 = CreateUser("daw_mya", "Daw Mya Sein (Management)", "daw.mya@asure.local", adminDeptId, UserRole.Management);
        var finance = CreateUser("ko_aung", "Ko Aung Naing (Finance)", "ko.aung@asure.local", adminDeptId, UserRole.Finance);
        var employee = CreateUser("ma_aye", "Ma Aye Aye (Employee)", "ma.aye@asure.local", itDeptId, UserRole.Employee);

        _db.Users.AddRange(admin, deptHeadIt, deptHeadHr, mgmt1, mgmt2, finance, employee);

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {DeptCount} departments and {UserCount} users.", 3, 7);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private User CreateUser(string username, string fullName, string email, Guid departmentId, UserRole role)
    {
        var user = new User(username, fullName, email, departmentId, role);
        user.SetPasswordHash(_passwordHasher.Hash(SeedPassword), mustChangeOnNextLogin: true);
        return user;
    }
}
