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
/// (e.g. currencies, COAs) will seed even on a database that already has users.
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
        await SeedAppSettingsAsync(cancellationToken);
        await SeedCurrenciesAsync(cancellationToken);
        await SeedCoasAsync(cancellationToken);
        await SeedWithdrawMethodsAsync(cancellationToken);
        await SeedBankAccountsAsync(cancellationToken);
        await SeedBudgetCategoriesAsync(cancellationToken);
        await SeedUsersAndDepartmentsAsync(cancellationToken);
    }

    // ── App Settings ──────────────────────────────────────────────────────────
    private async Task SeedAppSettingsAsync(CancellationToken cancellationToken)
    {
        if (await _db.AppSettings.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("App settings already seeded — skipping.");
            return;
        }

        _logger.LogInformation("Seeding app settings...");

        _db.AppSettings.Add(new AppSetting(
            "AdvanceBlackoutDays", 
            "3", 
            "Days before month-end when advance requests are blocked."));

        await _db.SaveChangesAsync(cancellationToken);
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

    // ── Chart of Accounts ─────────────────────────────────────────────────────
    private async Task SeedCoasAsync(CancellationToken cancellationToken)
    {
        if (await _db.Coas.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Chart of Accounts already seeded — skipping.");
            return;
        }

        _logger.LogInformation("Seeding chart of accounts...");

        _db.Coas.AddRange(
            new Coa("5110", "Office Supplies",   "Stationery, printer ink, small office goods."),
            new Coa("5120", "IT Equipment",      "Hardware purchases — laptops, monitors, peripherals."),
            new Coa("5130", "Software & SaaS",   "Software licenses, cloud subscriptions, SaaS tools."),
            new Coa("5210", "Travel & Transport","Local transport, mileage, taxi, fuel."),
            new Coa("5220", "Meals & Entertainment", "Client meals, team meals during travel."),
            new Coa("5310", "Training & Education",  "Courses, certifications, books for staff."),
            new Coa("5410", "Marketing & Advertising", "Campaigns, ads, promotional materials."),
            new Coa("6100", "Salaries & Wages",  "Payroll — regular salaries (HR / Finance use only)."),
            new Coa("6200", "Bonuses & Incentives", "Performance bonuses and incentive payouts."),
            new Coa("7100", "Utilities",         "Electricity, water, internet at office.")
        );

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ── Withdraw Methods ──────────────────────────────────────────────────────
    private async Task SeedWithdrawMethodsAsync(CancellationToken cancellationToken)
    {
        if (await _db.WithdrawMethods.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Withdraw methods already seeded — skipping.");
            return;
        }

        _logger.LogInformation("Seeding withdraw methods...");

        _db.WithdrawMethods.AddRange(
            new WithdrawMethod("Cash"),
            new WithdrawMethod("Bank Transfer", requiresAttachment: true),
            new WithdrawMethod("Cheque", requiresAttachment: true),
            new WithdrawMethod("Mobile Wallet", requiresAttachment: true)
        );

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ── Bank Accounts ─────────────────────────────────────────────────────────
    private async Task SeedBankAccountsAsync(CancellationToken cancellationToken)
    {
        if (await _db.BankAccounts.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Bank accounts already seeded — skipping.");
            return;
        }

        _logger.LogInformation("Seeding bank accounts...");

        _db.BankAccounts.AddRange(
            new BankAccount("KBZ Bank", "0012-3456-7890-1234", "aSure Co., Ltd."),
            new BankAccount("AYA Bank", "9988-7766-5544-3322", "aSure Co., Ltd.")
        );

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ── Budget Categories ─────────────────────────────────────────────────────
    private async Task SeedBudgetCategoriesAsync(CancellationToken cancellationToken)
    {
        if (await _db.BudgetCategories.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Budget categories already seeded — skipping.");
            return;
        }

        _logger.LogInformation("Seeding budget categories...");

        _db.BudgetCategories.AddRange(
            new BudgetCategory("Asset"),
            new BudgetCategory("Expense")
        );

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

        // Finance Approver (Type 1) — can approve, reject, send back, and record payments.
        var finance = CreateUser("ko_aung", "Ko Aung Naing (Finance Approver)", "ko.aung@asure.local", adminDeptId, UserRole.Finance);
        finance.SetFinanceApprover(true);

        // Finance Payer-only (Type 2) — can only record payments.
        var financePayer = CreateUser("daw_hla", "Daw Hla Win (Finance Payer)", "daw.hla@asure.local", adminDeptId, UserRole.Finance);

        var employee = CreateUser("ma_aye", "Ma Aye Aye (Employee)", "ma.aye@asure.local", itDeptId, UserRole.Employee);

        _db.Users.AddRange(admin, deptHeadIt, deptHeadHr, mgmt1, mgmt2, finance, financePayer, employee);

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {DeptCount} departments and {UserCount} users.", 3, 8);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private User CreateUser(string username, string fullName, string email, Guid departmentId, UserRole role)
    {
        var user = new User(username, fullName, email, departmentId, role);
        user.SetPasswordHash(_passwordHasher.Hash(SeedPassword), mustChangeOnNextLogin: true);
        return user;
    }
}
