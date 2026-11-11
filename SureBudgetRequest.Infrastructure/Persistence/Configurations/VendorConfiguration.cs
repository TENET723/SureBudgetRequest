using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("vendors");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Name).IsRequired().HasMaxLength(200);
        builder.Property(v => v.ContactPerson).HasMaxLength(200);
        builder.Property(v => v.Phone).HasMaxLength(50);
        builder.Property(v => v.Email).HasMaxLength(200);
        builder.Property(v => v.Address).HasMaxLength(500);
        builder.Property(v => v.IsActive).IsRequired();
        builder.Property(v => v.CreatedAt).IsRequired();

        builder.HasIndex(v => v.Name).IsUnique();

        builder.HasMany(v => v.PaymentAccounts)
               .WithOne()
               .HasForeignKey(a => a.VendorId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(v => v.PaymentAccounts)
               .HasField("_paymentAccounts")
               .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
