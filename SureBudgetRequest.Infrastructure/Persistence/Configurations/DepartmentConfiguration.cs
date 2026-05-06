using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name).IsRequired().HasMaxLength(200);
        builder.Property(d => d.HeadUserId).IsRequired();
        builder.Property(d => d.BudgetLimit).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(d => d.IsActive).IsRequired();
        builder.Property(d => d.CreatedAt).IsRequired();

        builder.HasIndex(d => d.Name).IsUnique();
    }
}
