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

        // Source bank account — nullable snapshot (filled for bank transfers,
        // null for cash). The id is kept for traceability; the name/number/holder
        // are denormalized so history survives edits/deactivation of the account.
        builder.Property(p => p.SourceBankAccountId).IsRequired(false);
        builder.Property(p => p.SourceBankName).HasMaxLength(200);
        builder.Property(p => p.SourceAccountNumber).HasMaxLength(100);
        builder.Property(p => p.SourceAccountHolderName).HasMaxLength(200);

        builder.HasIndex(p => p.BudgetRequestId);
        builder.HasIndex(p => p.SourceBankAccountId);

        // Navigation to receipt attachments — tells EF Core that Payment must
        // be INSERTed before any attachment UPDATE that sets payment_id.
        builder.HasMany(p => p.Receipts)
               .WithOne()
               .HasForeignKey(a => a.PaymentId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Receipts)
               .HasField("_receipts")
               .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
