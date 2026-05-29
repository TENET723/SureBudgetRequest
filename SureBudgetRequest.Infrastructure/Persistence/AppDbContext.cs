using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Infrastructure.Notifications;

namespace SureBudgetRequest.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Domain entities
    public DbSet<BudgetRequest> BudgetRequests => Set<BudgetRequest>();
    public DbSet<ApprovalAction> ApprovalActions => Set<ApprovalAction>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<AdvanceUsage> AdvanceUsages => Set<AdvanceUsage>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<CurrencyRateChange> CurrencyRateChanges => Set<CurrencyRateChange>();
    public DbSet<Coa> Coas => Set<Coa>();
    public DbSet<WithdrawMethod> WithdrawMethods => Set<WithdrawMethod>();
    public DbSet<BudgetCategory> BudgetCategories => Set<BudgetCategory>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<BudgetRequestModification> BudgetRequestModifications => Set<BudgetRequestModification>();

    // Infrastructure-only (outbox)
    public DbSet<NotificationOutboxEntry> NotificationOutbox => Set<NotificationOutboxEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all IEntityTypeConfiguration<T> classes from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
