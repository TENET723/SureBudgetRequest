using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("app_settings");

        builder.HasKey(s => s.Key);

        builder.Property(s => s.Key).HasMaxLength(100);
        builder.Property(s => s.Value).IsRequired().HasMaxLength(500);
        builder.Property(s => s.Description).HasMaxLength(1000);
    }
}
