using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("currencies");

        builder.HasKey(c => c.Code);
        builder.Property(c => c.Code).HasMaxLength(10);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.RateToMmk)
               .IsRequired()
               .HasColumnType("numeric(18,6)");
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.RateUpdatedAt).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
    }
}
