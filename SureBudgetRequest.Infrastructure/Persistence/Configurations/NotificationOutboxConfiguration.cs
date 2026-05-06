using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SureBudgetRequest.Infrastructure.Notifications;

namespace SureBudgetRequest.Infrastructure.Persistence.Configurations;

public class NotificationOutboxConfiguration : IEntityTypeConfiguration<NotificationOutboxEntry>
{
    public void Configure(EntityTypeBuilder<NotificationOutboxEntry> builder)
    {
        builder.ToTable("notification_outbox");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Payload).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.ProcessedAt);
        builder.Property(e => e.IsProcessed).IsRequired();
        builder.Property(e => e.RetryCount).IsRequired();
        builder.Property(e => e.LastError).HasMaxLength(2000);

        // The processor queries unprocessed entries — this index keeps it fast.
        builder.HasIndex(e => e.IsProcessed);
    }
}
