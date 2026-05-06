using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class ApprovalActionConfiguration : IEntityTypeConfiguration<ApprovalAction>
{
    public void Configure(EntityTypeBuilder<ApprovalAction> builder)
    {
        builder.ToTable("approval_actions");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.BudgetRequestId).IsRequired();
        builder.Property(a => a.Stage).IsRequired().HasConversion<int>();
        builder.Property(a => a.Decision).IsRequired().HasConversion<int>();
        builder.Property(a => a.ApproverId).IsRequired();
        builder.Property(a => a.Comment).HasMaxLength(1000);
        builder.Property(a => a.ActionedAt).IsRequired();

        builder.HasIndex(a => a.BudgetRequestId);
        builder.HasIndex(a => a.ApproverId);
    }
}
