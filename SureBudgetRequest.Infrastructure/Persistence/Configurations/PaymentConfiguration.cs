using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.BudgetRequestId).IsRequired();
        builder.Property(p => p.Amount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(p => p.PaidAt).IsRequired();
        builder.Property(p => p.RecordedByUserId).IsRequired();
        builder.Property(p => p.Reference).HasMaxLength(200);
        builder.Property(p => p.Note).HasMaxLength(1000);

        builder.HasIndex(p => p.BudgetRequestId);
    }
}
