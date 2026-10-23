using ChildScreenTime.Remote.Configuration;
using ChildScreenTime.Remote.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChildScreenTime.Remote.Services;

public sealed partial class RetentionCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<RemoteOptions> options,
    TimeProvider clock,
    ILogger<RetentionCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogCleanupFailure(logger, exception);
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = clock.GetUtcNow();
        var oldestUsageDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-options.Value.DataRetentionDays));
        var auditBoundary = now.AddDays(-366);
        var pairingBoundary = now.AddDays(-1);
        var usageDeleted = await database.UsageAggregates.Where(value => value.LocalDate < oldestUsageDate).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        var browserDeleted = await database.BrowserActivityAggregates.Where(value => value.LocalDate < oldestUsageDate).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await database.PairingCodes.Where(value => value.ExpiresUtc < pairingBoundary).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await database.SecurityAuditEvents.Where(value => value.TimestampUtc < auditBoundary).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        if (usageDeleted > 0)
        {
            LogUsageDeleted(logger, usageDeleted);
        }
        if (browserDeleted > 0)
        {
            LogBrowserActivityDeleted(logger, browserDeleted);
        }
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "Remote data retention cleanup failed.")]
    private static partial void LogCleanupFailure(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Removed {Count} expired usage aggregates.")]
    private static partial void LogUsageDeleted(ILogger logger, int count);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Removed {Count} expired YouTube activity aggregates.")]
    private static partial void LogBrowserActivityDeleted(ILogger logger, int count);
}
