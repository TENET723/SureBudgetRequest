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
        builder.Property(d => d.HeadUserId);   // nullable — department can exist without a head
        builder.Property(d => d.BudgetLimit).IsRequired().HasColumnType("numeric(18,2)");

        // Nullable. Notifications fail-loud and stay pending when missing —
        // see NotificationOutboxProcessor for the routing logic.
        builder.Property(d => d.SlackWebhookUrl).HasMaxLength(500);

        builder.Property(d => d.IsActive).IsRequired();
        builder.Property(d => d.CreatedAt).IsRequired();

        builder.HasIndex(d => d.Name).IsUnique();
    }
}
