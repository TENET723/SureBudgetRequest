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

        builder.HasIndex(m => m.Name).IsUnique();
    }
}
