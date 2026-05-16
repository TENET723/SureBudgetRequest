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
/// entries and POSTs them to the Slack webhook OF THE REQUESTER'S DEPARTMENT.
/// Retries up to MaxRetries times before giving up.
///
/// At send time the processor loads:
///   - the BudgetRequest (for ID/Reference, Type, Reasons, Amount, etc.)
///   - the Department (for its current Name AND its SlackWebhookUrl)
///   - the active recipients' SlackUserId values (for @mentions)
/// then hands the lot to SlackMessageBuilder. Loading fresh means an admin
/// fixing a typo (wrong Slack ID, wrong department name, wrong webhook URL)
/// applies even to already-queued rows.
///
/// MISSING-WEBHOOK BEHAVIOUR (Option Y):
/// When the requester's department has no SlackWebhookUrl configured the entry
/// is left PENDING — RetryCount is NOT bumped — and a warning is logged on every
/// poll cycle. The notification will deliver automatically once an admin sets
/// the webhook in Admin → Departments. We trade log noise for guaranteed
/// delivery; misconfiguration stays visible.
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
        _logger.LogInformation(
            "NotificationOutboxProcessor started. Routing per-department: webhook URL is read from Department.SlackWebhookUrl at send time.");

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
            .Where(e => !e.IsProcessed && e.RetryCount < _options.MaxRetries)
            .OrderBy(e => e.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        _logger.LogDebug("Processing {Count} outbox notification(s).", pending.Count);

        // Track whether anything changed this cycle. Skipping pending-due-to-missing-webhook
        // does NOT mutate the entry, so on cycles where every entry is skipped we avoid an
        // unnecessary SaveChanges roundtrip.
        var hasMutations = false;

        foreach (var entry in pending)
        {
            try
            {
                var notification = JsonSerializer.Deserialize<NotificationEvent>(entry.Payload)
                    ?? throw new InvalidOperationException("Failed to deserialize NotificationEvent.");

                // Load the request and its department fresh — these carry the
                // bulk of the message body (Reference, Type, Reasons, etc.) AND
                // the webhook URL we POST to.
                var budgetRequest = await db.BudgetRequests
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == notification.BudgetRequestId, ct);

                if (budgetRequest is null)
                {
                    entry.RecordFailure($"BudgetRequest {notification.BudgetRequestId} not found.");
                    hasMutations = true;
                    _logger.LogWarning(
                        "Skipping outbox entry {EntryId}: BudgetRequest {RequestId} not found.",
                        entry.Id, notification.BudgetRequestId);
                    continue;
                }

                var department = await db.Departments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == budgetRequest.DepartmentIdAtSubmission, ct);

                var departmentName = department?.Name ?? "—";
                var webhookUrl = department?.SlackWebhookUrl;

                // OPTION Y: department has no webhook configured. Leave the entry
                // pending (do NOT bump RetryCount) so it goes out as soon as an
                // admin sets the URL. Log loudly so misconfiguration is visible.
                if (string.IsNullOrWhiteSpace(webhookUrl))
                {
                    _logger.LogWarning(
                        "No Slack webhook configured for department '{DepartmentName}' (id {DepartmentId}). " +
                        "Outbox entry {EntryId} for request {RequestId} (trigger: {Trigger}) is left pending. " +
                        "Configure the webhook in Admin → Departments to deliver this and any other queued notifications.",
                        departmentName,
                        budgetRequest.DepartmentIdAtSubmission,
                        entry.Id,
                        notification.BudgetRequestId,
                        notification.Trigger);
                    continue;
                }

                var mentions = await BuildMentionsAsync(db, notification, ct);

                var slackPayload = SlackMessageBuilder.Build(
                    notification, budgetRequest, departmentName, mentions);

                var content = new StringContent(slackPayload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(webhookUrl, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    entry.MarkProcessed();
                    hasMutations = true;
                    _logger.LogInformation(
                        "Slack notification sent for request {RequestId} to '{DepartmentName}' channel (trigger: {Trigger}, recipients: {Count}).",
                        notification.BudgetRequestId,
                        departmentName,
                        notification.Trigger,
                        notification.RecipientUserIds.Count);
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    var error = $"HTTP {(int)response.StatusCode}: {body}";
                    entry.RecordFailure(error);
                    hasMutations = true;
                    _logger.LogWarning(
                        "Slack POST failed for outbox entry {EntryId} (department '{DepartmentName}'): {Error}",
                        entry.Id, departmentName, error);
                }
            }
            catch (Exception ex)
            {
                entry.RecordFailure(ex.Message);
                hasMutations = true;
                _logger.LogWarning(
                    ex,
                    "Exception while processing outbox entry {EntryId}.",
                    entry.Id);
            }
        }

        if (hasMutations)
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
