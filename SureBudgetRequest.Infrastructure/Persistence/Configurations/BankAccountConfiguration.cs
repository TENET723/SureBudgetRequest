using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("bank_accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.BankName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.AccountNumber).IsRequired().HasMaxLength(100);
        builder.Property(a => a.AccountHolderName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.IsActive).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
    }
}
