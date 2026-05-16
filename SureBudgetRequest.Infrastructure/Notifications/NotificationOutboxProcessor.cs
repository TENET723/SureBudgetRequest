using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Infrastructure.Persistence;

namespace SureBudgetRequest.Infrastructure.Notifications;

/// <summary>
/// Hosted background service that polls NotificationOutboxEntry for unprocessed
/// entries and POSTs them to the Slack webhook. Retries up to MaxRetries times
/// before giving up.
///
/// At send time the processor loads:
///   - the BudgetRequest (for ID/Reference, Type, Reasons, Amount, etc.)
///   - the Department (for its current Name)
///   - the active recipients' SlackUserId values (for @mentions)
/// then hands the lot to SlackMessageBuilder. Loading fresh means an admin
/// fixing a typo (wrong Slack ID, wrong department name) applies even to
/// already-queued rows.
/// </summary>
public sealed class NotificationOutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HttpClient _httpClient;
    private readonly SlackOptions _options;
    private readonly ILogger<NotificationOutboxProcessor> _logger;

    public NotificationOutboxProcessor(
        IServiceScopeFactory scopeFactory,
        HttpClient httpClient,
        IOptions<SlackOptions> options,
        ILogger<NotificationOutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationOutboxProcessor started.");
        _logger.LogWarning(
            "Slack webhook in use: [{Url}] (length: {Length})",
            _options.WebhookUrl,
            _options.WebhookUrl?.Length ?? 0);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingEntriesAsync(stoppingToken);

            await Task.Delay(
                TimeSpan.FromSeconds(_options.PollingIntervalSeconds),
                stoppingToken);
        }
    }

    private async Task ProcessPendingEntriesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await db.NotificationOutbox
            .Where(e => !e.IsProcessed
            && e.RetryCount < _options.MaxRetries
            )
            .OrderBy(e => e.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        _logger.LogDebug("Processing {Count} outbox notification(s).", pending.Count);

        foreach (var entry in pending)
        {
            try
            {
                var notification = JsonSerializer.Deserialize<NotificationEvent>(entry.Payload)
                    ?? throw new InvalidOperationException("Failed to deserialize NotificationEvent.");

                // Load the request and its department fresh — these carry the
                // bulk of the message body (Reference, Type, Reasons, etc.).
                var budgetRequest = await db.BudgetRequests
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == notification.BudgetRequestId, ct);

                if (budgetRequest is null)
                {
                    entry.RecordFailure($"BudgetRequest {notification.BudgetRequestId} not found.");
                    _logger.LogWarning(
                        "Skipping outbox entry {EntryId}: BudgetRequest {RequestId} not found.",
                        entry.Id, notification.BudgetRequestId);
                    continue;
                }

                var department = await db.Departments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == budgetRequest.DepartmentIdAtSubmission, ct);

                var departmentName = department?.Name ?? "—";

                var mentions = await BuildMentionsAsync(db, notification, ct);

                var slackPayload = SlackMessageBuilder.Build(
                    notification, budgetRequest, departmentName, mentions);

                var content = new StringContent(slackPayload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_options.WebhookUrl, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    entry.MarkProcessed();
                    _logger.LogInformation(
                        "Slack notification sent for request {RequestId} (trigger: {Trigger}, recipients: {Count}).",
                        notification.BudgetRequestId,
                        notification.Trigger,
                        notification.RecipientUserIds.Count);
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    var error = $"HTTP {(int)response.StatusCode}: {body}";
                    entry.RecordFailure(error);
                    _logger.LogWarning(
                        "Slack POST failed for outbox entry {EntryId}: {Error}",
                        entry.Id, error);
                }
            }
            catch (Exception ex)
            {
                entry.RecordFailure(ex.Message);
                _logger.LogWarning(
                    ex,
                    "Exception while processing outbox entry {EntryId}.",
                    entry.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Loads the SlackUserId for each active recipient and wraps it as
    /// "&lt;@SlackId&gt;". Users without a SlackUserId or who are inactive are
    /// silently skipped. Returns an empty list when no one can be mentioned.
    /// </summary>
    private static async Task<IReadOnlyList<string>> BuildMentionsAsync(
        AppDbContext db,
        NotificationEvent notification,
        CancellationToken ct)
    {
        if (notification.RecipientUserIds.Count == 0)
            return Array.Empty<string>();

        var ids = notification.RecipientUserIds.ToArray();

        var slackIds = await db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.SlackUserId != null && u.IsActive)
            .Select(u => u.SlackUserId!)
            .ToArrayAsync(ct);

        return slackIds.Select(id => $"<@{id}>").ToArray();
    }
}
