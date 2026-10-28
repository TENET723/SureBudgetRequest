using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.DepartmentId).IsRequired();
        builder.Property(t => t.Name).IsRequired().HasMaxLength(50);
        builder.Property(t => t.ColorHex).HasMaxLength(10);
        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.CreatedByUserId).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();

        builder.HasIndex(t => new { t.DepartmentId, t.Name }).IsUnique();

        builder.HasOne<Department>()
               .WithMany()
               .HasForeignKey(t => t.DepartmentId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(t => t.CreatedByUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
