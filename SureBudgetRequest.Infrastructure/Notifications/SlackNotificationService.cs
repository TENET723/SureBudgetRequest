using System.Text.Json;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Common;

namespace SureBudgetRequest.Infrastructure.Notifications;

/// <summary>
/// Implements <see cref="INotificationService"/> by writing to the
/// <see cref="NotificationOutboxEntry"/> table within the current EF Core
/// transaction. The actual HTTP POST to Slack is handled by
/// <see cref="NotificationOutboxProcessor"/> running as a background service.
///
/// This ensures that a Slack outage never loses a notification — the outbox
/// entry is committed atomically with the domain change.
/// </summary>
public sealed class SlackNotificationService : INotificationService
{
    private readonly Persistence.AppDbContext _context;

    public SlackNotificationService(Persistence.AppDbContext context)
        => _context = context;

    public async Task SendAsync(
        NotificationEvent notification,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(notification);
        var entry = new NotificationOutboxEntry(payload);
        await _context.NotificationOutbox.AddAsync(entry, cancellationToken);
        // NOTE: SaveChanges is NOT called here. The Application layer's
        // IUnitOfWork.SaveChangesAsync() commits both the domain change
        // and this outbox entry in a single transaction.
    }
}
