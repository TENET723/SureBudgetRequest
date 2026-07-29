using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SureBudgetRequest.Application.Abstractions.Services;

namespace SureBudgetRequest.Infrastructure.Storage;

/// <summary>
/// Background service that periodically cleans up expired temporary file uploads from storage.
/// Resolves the scoped <see cref="IFileStorage"/> dependency on each run cycle.
/// </summary>
public sealed class TempCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<StorageOptions> _options;
    private readonly ILogger<TempCleanupBackgroundService> _logger;

    public TempCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<StorageOptions> options,
        ILogger<TempCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cleanInterval = TimeSpan.FromHours(_options.Value.TempCleanIntervalHours);
        var expiration = TimeSpan.FromHours(_options.Value.TempExpirationHours);

        _logger.LogInformation(
            "TempCleanupBackgroundService started. Cleanup interval: {Interval} hours. Expiration threshold: {Expiration} hours.",
            _options.Value.TempCleanIntervalHours,
            _options.Value.TempExpirationHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

                _logger.LogInformation("Starting temporary file cleanup...");
                await fileStorage.CleanTempFilesAsync(expiration, stoppingToken);
                _logger.LogInformation("Temporary file cleanup completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during temporary file cleanup.");
            }

            try
            {
                await Task.Delay(cleanInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
        }
    }
}
