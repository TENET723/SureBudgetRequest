using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username).IsRequired().HasMaxLength(100);
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(300);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
        builder.Property(u => u.MustChangePassword).IsRequired();
        builder.Property(u => u.SlackUserId).HasMaxLength(50);
        builder.Property(u => u.DepartmentId).IsRequired();
        builder.Property(u => u.Role).IsRequired().HasConversion<int>();
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();

        builder.HasIndex(u => u.Username).IsUnique();

        // Case-insensitive uniqueness is enforced at the application layer
        // (User entity stores email lowercase + trimmed).
        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasIndex(u => u.DepartmentId);
        builder.HasIndex(u => u.Role); // for fast role-based lookups
    }
}
