using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class BudgetRequestTagConfiguration : IEntityTypeConfiguration<BudgetRequestTag>
{
    public void Configure(EntityTypeBuilder<BudgetRequestTag> builder)
    {
        builder.ToTable("budget_request_tags");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.BudgetRequestId).IsRequired();
        builder.Property(t => t.TagId).IsRequired();
        builder.Property(t => t.TagDepartmentId).IsRequired();
        builder.Property(t => t.AttachedByUserId).IsRequired();
        builder.Property(t => t.AttachedAt).IsRequired();

        builder.HasIndex(t => new { t.BudgetRequestId, t.TagId }).IsUnique();
        builder.HasIndex(t => new { t.BudgetRequestId, t.TagDepartmentId });

        builder.HasOne(t => t.Tag)
               .WithMany()
               .HasForeignKey(t => t.TagId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(t => t.AttachedByUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
