namespace SureBudgetRequest.Infrastructure.Notifications;

/// <summary>
/// An Infrastructure-only entity that stores a pending Slack notification.
/// Written in the same transaction as the domain command so that a Slack
/// outage never causes a missed notification (outbox pattern).
/// </summary>
public sealed class NotificationOutboxEntry
{
    public Guid Id { get; private set; } = Guid.Empty;

    /// <summary>JSON-serialized <see cref="Application.BudgetRequests.Common.NotificationEvent"/>.</summary>
    public string Payload { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; private set; }
    public bool IsProcessed { get; private set; }
    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }

    // For EF Core
    private NotificationOutboxEntry() { }

    public NotificationOutboxEntry(string payload)
    {
        Payload = payload;
    }

    public void MarkProcessed()
    {
        IsProcessed = true;
        ProcessedAt = DateTime.UtcNow;
    }

    public void RecordFailure(string error)
    {
        RetryCount++;
        LastError = error;
    }
}
