namespace SureBudgetRequest.Domain.Entities;

public class Department
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public Guid? HeadUserId { get; private set; }

    /// <summary>
    /// Per-request budget limit in MMK. A request whose MMK-equivalent amount
    /// exceeds this value is routed through the Management stage at submission.
    /// </summary>
    public decimal BudgetLimit { get; private set; }

    /// <summary>
    /// Optional monthly spending limit in MMK. When set, submission requires a
    /// justification if the current month's spend plus the new request would
    /// exceed this value. <c>null</c> means the department has no monthly limit
    /// configured and the monthly check is skipped entirely (backwards-compatible
    /// for departments that existed before this feature).
    ///
    /// Setting this to a non-null value (including 0) enables enforcement.
    /// </summary>
    public decimal? MonthlyLimit { get; private set; }

    /// <summary>
    /// Slack incoming webhook URL for this department's channel. Notifications
    /// for budget requests originating from this department are POSTed here.
    /// Nullable — when not configured the outbox processor logs a warning and
    /// leaves the notification pending until an admin sets the URL.
    /// </summary>
    public string? SlackWebhookUrl { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // For EF Core
    private Department() { }

    public Department(
        string name,
        Guid? headUserId,
        decimal budgetLimit,
        decimal? monthlyLimit = null,
        string? slackWebhookUrl = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Department name is required.", nameof(name));
        if (budgetLimit < 0)
            throw new ArgumentException("Budget limit cannot be negative.", nameof(budgetLimit));
        if (monthlyLimit.HasValue && monthlyLimit.Value < 0)
            throw new ArgumentException("Monthly limit cannot be negative.", nameof(monthlyLimit));

        //Id = Guid.NewGuid();
        Name = name;
        HeadUserId = headUserId;
        BudgetLimit = budgetLimit;
        MonthlyLimit = monthlyLimit;
        SlackWebhookUrl = NormalizeAndValidateWebhook(slackWebhookUrl);
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Department name is required.", nameof(newName));
        Name = newName;
    }

    public void ChangeHead(Guid? newHeadUserId) => HeadUserId = newHeadUserId;

    public void ChangeBudgetLimit(decimal newLimit)
    {
        if (newLimit < 0)
            throw new ArgumentException("Budget limit cannot be negative.", nameof(newLimit));
        BudgetLimit = newLimit;
    }

    /// <summary>
    /// Sets or clears the monthly spending limit. Pass <c>null</c> to disable
    /// monthly enforcement entirely. Pass any non-negative value to enable it.
    /// </summary>
    public void ChangeMonthlyLimit(decimal? newLimit)
    {
        if (newLimit.HasValue && newLimit.Value < 0)
            throw new ArgumentException("Monthly limit cannot be negative.", nameof(newLimit));
        MonthlyLimit = newLimit;
    }

    /// <summary>
    /// Sets or clears the Slack webhook URL. Pass null or whitespace to clear.
    /// Throws <see cref="ArgumentException"/> if the value is non-empty and
    /// does not look like a valid Slack incoming webhook URL.
    /// </summary>
    public void SetSlackWebhookUrl(string? url) =>
        SlackWebhookUrl = NormalizeAndValidateWebhook(url);

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;

    // ── Webhook validation ────────────────────────────────────────────────────

    private const string SlackWebhookPrefix = "https://hooks.slack.com/services/";

    private static string? NormalizeAndValidateWebhook(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (!trimmed.StartsWith(SlackWebhookPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Slack webhook URL must start with '{SlackWebhookPrefix}'.",
                nameof(url));
        return trimmed;
    }
}
