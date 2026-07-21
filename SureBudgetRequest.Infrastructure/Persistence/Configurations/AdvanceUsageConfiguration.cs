using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class AdvanceUsageConfiguration : IEntityTypeConfiguration<AdvanceUsage>
{
    public void Configure(EntityTypeBuilder<AdvanceUsage> builder)
    {
        builder.ToTable("advance_usages");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.BudgetRequestId).IsRequired();
        builder.Property(u => u.SpentOn).IsRequired();
        builder.Property(u => u.Amount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(u => u.Description).IsRequired().HasMaxLength(1000);
        builder.Property(u => u.RecordedAt).IsRequired();
        builder.Property(u => u.RecordedByUserId).IsRequired();

        // The BudgetRequest → AdvanceUsages relationship (incl. its FK delete
        // behaviour) is configured on the BudgetRequest side in
        // BudgetRequestConfiguration, mirroring how Payments are wired.

        builder.HasIndex(u => u.BudgetRequestId);

        // Navigation to receipt attachments — tells EF Core that AdvanceUsage must
        // be INSERTed before any attachment UPDATE that sets advance_usage_id.
        builder.HasMany(u => u.Receipts)
               .WithOne()
               .HasForeignKey(a => a.AdvanceUsageId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(u => u.Receipts)
               .HasField("_receipts")
               .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
