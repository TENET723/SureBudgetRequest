using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class DepartmentBudgetPeriodConfiguration : IEntityTypeConfiguration<DepartmentBudgetPeriod>
{
    public void Configure(EntityTypeBuilder<DepartmentBudgetPeriod> builder)
    {
        builder.ToTable("department_budget_periods");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.DepartmentId).IsRequired();
        builder.Property(p => p.EffectiveFromFinancialYear).IsRequired();

        builder.Property(p => p.PeriodType)
               .IsRequired()
               .HasConversion<int>();   // store as int, not PG enum (easier to evolve)

        builder.Property(p => p.LimitAmount)
               .IsRequired()
               .HasColumnType("numeric(18,2)");

        builder.Property(p => p.CreatedAt).IsRequired();

        // At most one config row per department per financial year.
        builder.HasIndex(p => new { p.DepartmentId, p.EffectiveFromFinancialYear }).IsUnique();

        // FK to the owning department. Cascade — the config is owned by the
        // department and carries no historical snapshot (those live on
        // budget_requests), so it can be cleaned up if a department is ever deleted.
        builder.HasOne<Department>()
               .WithMany()
               .HasForeignKey(p => p.DepartmentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
