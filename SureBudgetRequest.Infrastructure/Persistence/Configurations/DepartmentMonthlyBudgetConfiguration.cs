using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class DepartmentMonthlyBudgetConfiguration : IEntityTypeConfiguration<DepartmentMonthlyBudget>
{
    public void Configure(EntityTypeBuilder<DepartmentMonthlyBudget> builder)
    {
        builder.ToTable("department_monthly_budgets");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.DepartmentId).IsRequired();
        builder.Property(b => b.Year).IsRequired();
        builder.Property(b => b.Month).IsRequired();
        builder.Property(b => b.Amount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(b => b.CreatedByUserId).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.UpdatedByUserId);
        builder.Property(b => b.UpdatedAt);

        builder.HasIndex(b => new { b.DepartmentId, b.Year, b.Month }).IsUnique();

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(b => b.DepartmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
