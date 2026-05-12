using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class CurrencyRateChangeConfiguration : IEntityTypeConfiguration<CurrencyRateChange>
{
    public void Configure(EntityTypeBuilder<CurrencyRateChange> builder)
    {
        builder.ToTable("currency_rate_changes");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CurrencyCode).IsRequired().HasMaxLength(10);
        builder.Property(c => c.OldRate).IsRequired().HasColumnType("numeric(18,6)");
        builder.Property(c => c.NewRate).IsRequired().HasColumnType("numeric(18,6)");
        builder.Property(c => c.ChangedByUserId).IsRequired();
        builder.Property(c => c.ChangedAt).IsRequired();

        builder.HasIndex(c => c.CurrencyCode);
        builder.HasIndex(c => c.ChangedAt);

        // FK to currencies.code (Restrict so a currency can't be hard-deleted while history exists)
        builder.HasOne<Currency>()
               .WithMany()
               .HasForeignKey(c => c.CurrencyCode)
               .HasPrincipalKey(c => c.Code)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
