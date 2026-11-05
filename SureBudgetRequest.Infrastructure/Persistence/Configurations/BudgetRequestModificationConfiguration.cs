using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class BudgetRequestModificationConfiguration : IEntityTypeConfiguration<BudgetRequestModification>
{
    public void Configure(EntityTypeBuilder<BudgetRequestModification> builder)
    {
        builder.ToTable("budget_request_modifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BudgetRequestId).IsRequired();
        builder.Property(x => x.ModifiedByUserId).IsRequired();
        builder.Property(x => x.ModifiedAt).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.FallbackUserId);

        // FK to BudgetRequests
        builder.HasOne<BudgetRequest>()
            .WithMany()
            .HasForeignKey(x => x.BudgetRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to Users (ModifiedBy)
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ModifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to Users (FallbackUser)
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.FallbackUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.BudgetRequestId);
        builder.HasIndex(x => x.ModifiedAt);
        builder.HasIndex(x => x.FallbackUserId);
    }
}
