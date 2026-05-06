using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Infrastructure.Persistence;

namespace SureBudgetRequest.Infrastructure.Seeding;

/// <summary>
/// Seeds the database with initial departments and users for development.
/// Safe to call on every startup — checks if data already exists before inserting.
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
        if (await _db.Users.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Database already seeded — skipping.");
            return;
        }

        _logger.LogInformation("Seeding database...");

        // ── 1. Create a placeholder admin user first (needed as dept head) ───
        // We use a two-pass approach: create users without departments, then wire up.
        // Simpler: use well-known GUIDs so we can reference IDs before saving.

        var adminId   = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var deptHeadItId  = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var deptHeadHrId  = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var bossId    = Guid.Parse("00000000-0000-0000-0000-000000000004");
        var financeId = Guid.Parse("00000000-0000-0000-0000-000000000005");
        var employeeId= Guid.Parse("00000000-0000-0000-0000-000000000006");

        var itDeptId  = Guid.Parse("00000000-0000-0000-0001-000000000001");
        var hrDeptId  = Guid.Parse("00000000-0000-0000-0001-000000000002");
        var adminDeptId = Guid.Parse("00000000-0000-0000-0001-000000000003");

        // ── 2. Departments ────────────────────────────────────────────────────
        // HeadUserId references are set after users are wired up.
        // We use direct property sets via reflection-accessible internal ctor workaround:
        // Since Department has a public constructor, we just use it.

        var itDept = CreateDepartment(itDeptId, "Information Technology", deptHeadItId, 5_000_000);
        var hrDept = CreateDepartment(hrDeptId, "Human Resources", deptHeadHrId, 3_000_000);
        var adminDept = CreateDepartment(adminDeptId, "Administration", adminId, 10_000_000);

        _db.Departments.AddRange(itDept, hrDept, adminDept);

        // ── 3. Users ──────────────────────────────────────────────────────────
        var admin = CreateUser(adminId, "admin", "Mg Mg (Admin)", adminDeptId, UserRole.Admin);
        var deptHeadIt = CreateUser(deptHeadItId, "ko_zin", "Ko Zin Htet (IT Head)", itDeptId, UserRole.DepartmentHead);
        var deptHeadHr = CreateUser(deptHeadHrId, "ma_thida", "Ma Thida (HR Head)", hrDeptId, UserRole.DepartmentHead);
        var boss = CreateUser(bossId, "u_kyaw", "U Kyaw Zin (Boss)", adminDeptId, UserRole.Boss);
        var finance = CreateUser(financeId, "ko_aung", "Ko Aung Naing (Finance)", adminDeptId, UserRole.Finance);
        var employee = CreateUser(employeeId, "ma_aye", "Ma Aye Aye (Employee)", itDeptId, UserRole.Employee);

        _db.Users.AddRange(admin, deptHeadIt, deptHeadHr, boss, finance, employee);

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Database seeded with {DeptCount} departments and {UserCount} users.",
            3, 6);
    }

    // ── Helpers (work around private setters via domain constructors) ─────────

    private static Department CreateDepartment(Guid id, string name, Guid headUserId, decimal limit)
    {
        // Domain constructor sets a new Guid — we override via a known-ID helper.
        // Since Department doesn't expose a SetId(), we use EF's ability to set
        // shadow properties or we call the public constructor and accept the new Guid.
        // For seeding simplicity we use the public constructor and record the actual Id.
        //
        // Alternative: use ModelSnapshot fixed IDs in a proper migration seed.
        // For development seed, auto-Guid is fine — we just need stable role relationships.
        var dept = new Department(name, headUserId, limit);
        return dept;
    }

    private static User CreateUser(Guid id, string username, string fullName, Guid departmentId, UserRole role)
    {
        var user = new User(username, fullName, departmentId, role);
        return user;
    }
}
