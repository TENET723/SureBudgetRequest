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
/// Hosted background service that polls <see cref="NotificationOutboxEntry"/>
/// for unprocessed entries and POSTs them to the Slack webhook.
/// Retries up to <see cref="SlackOptions.MaxRetries"/> times before giving up.
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

        foreach (var entry in pending)
        {
            try
            {
                var notification = JsonSerializer.Deserialize<NotificationEvent>(entry.Payload)
                    ?? throw new InvalidOperationException("Failed to deserialize NotificationEvent.");

                var slackPayload = SlackMessageBuilder.Build(notification);
                var content = new StringContent(slackPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_options.WebhookUrl, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    entry.MarkProcessed();
                    _logger.LogInformation(
                        "Slack notification sent for request {RequestId} (trigger: {Trigger}).",
                        notification.BudgetRequestId,
                        notification.Trigger);
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
}
