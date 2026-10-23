using ChildScreenTime.Core.Models;
using ChildScreenTime.Core.Services;

namespace ChildScreenTime.Windows.Services;

public sealed record MonitoringStatus(
    DateTimeOffset Timestamp,
    string CurrentApplication,
    long ActiveSecondsToday,
    RestrictionDecision Decision);

public sealed class MonitoringCoordinator : IAsyncDisposable
{
    private const int SampleSeconds = 5;
    private static readonly TimeSpan MaximumIdleTime = TimeSpan.FromMinutes(5);

    private readonly SettingsRepository settingsRepository;
    private readonly UsageRepository usageRepository;
    private readonly BrowserActivityRepository browserActivityRepository;
    private readonly AuditRepository auditRepository;
    private readonly WindowsActivityProbe activityProbe;
    private readonly CancellationTokenSource cancellation = new();
    private Task? monitoringTask;
    private RestrictionReason previousReason;

    public MonitoringCoordinator(
        SettingsRepository settingsRepository,
        UsageRepository usageRepository,
        BrowserActivityRepository browserActivityRepository,
        AuditRepository auditRepository,
        WindowsActivityProbe activityProbe)
    {
        this.settingsRepository = settingsRepository;
        this.usageRepository = usageRepository;
        this.browserActivityRepository = browserActivityRepository;
        this.auditRepository = auditRepository;
        this.activityProbe = activityProbe;
    }

    public event EventHandler<MonitoringStatus>? StatusChanged;

    public void Start()
    {
        monitoringTask ??= Task.Run(() => MonitorAsync(cancellation.Token));
    }

    public MonitoringStatus GetCurrentStatus()
    {
        var date = DateOnly.FromDateTime(DateTime.Today);
        var settings = settingsRepository.GetSnapshot();
        var application = activityProbe.GetForegroundApplication();
        var totalSeconds = usageRepository.GetTotalSeconds(date);
        var appSeconds = application is null ? 0 : usageRepository.GetProcessSeconds(date, application.ProcessName);
        var decision = RestrictionEvaluator.Evaluate(
            settings,
            DateTimeOffset.Now,
            application?.ProcessName ?? string.Empty,
            totalSeconds,
            appSeconds);
        return new(DateTimeOffset.Now, application?.DisplayName ?? "No active application", totalSeconds, decision);
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(SampleSeconds));
        var ticksSinceFlush = 0;

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var date = DateOnly.FromDateTime(DateTime.Today);
                var settings = settingsRepository.GetSnapshot();
                var application = activityProbe.GetForegroundApplication();

                if (settings.MonitoringEnabled &&
                    application is not null &&
                    WindowsActivityProbe.IsUserActive(MaximumIdleTime))
                {
                    usageRepository.AddSample(date, application, SampleSeconds);
                }

                var totalSeconds = usageRepository.GetTotalSeconds(date);
                var appSeconds = application is null ? 0 : usageRepository.GetProcessSeconds(date, application.ProcessName);
                var decision = RestrictionEvaluator.Evaluate(
                    settings,
                    DateTimeOffset.Now,
                    application?.ProcessName ?? string.Empty,
                    totalSeconds,
                    appSeconds);

                if (decision.Reason != previousReason)
                {
                    auditRepository.Add(
                        decision.IsBlocked ? "restriction_started" : "restriction_ended",
                        decision.IsBlocked ? decision.Message : "PC access is available.");
                    previousReason = decision.Reason;
                }

                StatusChanged?.Invoke(this, new MonitoringStatus(
                    DateTimeOffset.Now,
                    application?.DisplayName ?? "No active application",
                    totalSeconds,
                    decision));

                ticksSinceFlush++;
                if (ticksSinceFlush >= 6)
                {
                    usageRepository.FlushAndPrune(settings.RetentionDays);
                    browserActivityRepository.ImportInboxAndPrune(settings.RetentionDays);
                    ticksSinceFlush = 0;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        finally
        {
            var retentionDays = settingsRepository.GetSnapshot().RetentionDays;
            usageRepository.FlushAndPrune(retentionDays);
            browserActivityRepository.ImportInboxAndPrune(retentionDays);
        }
    }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        if (monitoringTask is not null)
        {
            await monitoringTask.ConfigureAwait(false);
        }
        cancellation.Dispose();
    }
}
