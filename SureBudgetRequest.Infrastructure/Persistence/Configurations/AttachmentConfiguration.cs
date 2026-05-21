using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.BudgetRequestId).IsRequired();
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(500);
        builder.Property(a => a.StoredPath).IsRequired().HasMaxLength(1000);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(200);
        builder.Property(a => a.SizeBytes).IsRequired();
        builder.Property(a => a.UploadedByUserId).IsRequired();
        builder.Property(a => a.UploadedAt).IsRequired();

        // Stored as int; default 0 (General) so existing rows backfill correctly.
        builder.Property(a => a.Category)
               .IsRequired()
               .HasConversion<int>()
               .HasDefaultValue(Domain.Enums.AttachmentCategory.General);

        builder.HasIndex(a => a.BudgetRequestId);
    }
}
