namespace SureBudgetRequest.Infrastructure.Notifications;

public sealed class SlackOptions
{
    public const string SectionName = "Slack";

    /// <summary>Slack incoming webhook URL, e.g. https://hooks.slack.com/services/...</summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>Maximum number of send attempts before an outbox entry is abandoned.</summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>How often the outbox processor polls for pending entries (seconds).</summary>
    public int PollingIntervalSeconds { get; set; } = 10;
}
