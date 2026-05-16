namespace SureBudgetRequest.Infrastructure.Notifications;

public sealed class SlackOptions
{
    public const string SectionName = "Slack";

    /// <summary>
    /// Maximum number of send attempts before an outbox entry is abandoned.
    /// Note: this does NOT apply to "missing webhook" outcomes — those leave the
    /// entry pending indefinitely so it delivers once an admin configures the URL
    /// in Admin → Departments (see <see cref="NotificationOutboxProcessor"/>).
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>How often the outbox processor polls for pending entries (seconds).</summary>
    public int PollingIntervalSeconds { get; set; } = 10;
}
