using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class WithdrawMethodConfiguration : IEntityTypeConfiguration<WithdrawMethod>
{
    public void Configure(EntityTypeBuilder<WithdrawMethod> builder)
    {
        builder.ToTable("withdraw_methods");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);
        builder.Property(m => m.IsActive).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();

        // Default false so existing rows (and the migration's NOT NULL column add)
        // get the same value without a backfill pass.
        builder.Property(m => m.RequiresAttachment)
               .IsRequired()
               .HasDefaultValue(false);

        builder.HasIndex(m => m.Name).IsUnique();
    }
}
