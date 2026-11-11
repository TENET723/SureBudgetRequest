using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class VendorPaymentAccountConfiguration : IEntityTypeConfiguration<VendorPaymentAccount>
{
    public void Configure(EntityTypeBuilder<VendorPaymentAccount> builder)
    {
        builder.ToTable("vendor_payment_accounts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.VendorId).IsRequired();
        builder.Property(a => a.AccountType).IsRequired().HasConversion<int>();
        builder.Property(a => a.ProviderName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.AccountNumber).IsRequired().HasMaxLength(100);
        builder.Property(a => a.AccountHolderName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.BranchName).HasMaxLength(200);
        builder.Property(a => a.IsDefault).IsRequired();
        builder.Property(a => a.IsActive).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.Property(a => a.AttachmentFileName).HasMaxLength(255);
        builder.Property(a => a.AttachmentStoredPath).HasMaxLength(500);
        builder.Property(a => a.AttachmentContentType).HasMaxLength(100);
        builder.Property(a => a.AttachmentSizeBytes);

        builder.Ignore(a => a.HasAttachment);

        builder.HasIndex(a => a.VendorId);
    }
}
