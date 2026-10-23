using ChildScreenTime.Windows.Services;

namespace ChildScreenTime.Windows;

public sealed class AppServices
{
    public AppServices()
    {
        Paths = new AppDataPaths();
        var protectedStore = new DpapiFileStore();
        Settings = new SettingsRepository(protectedStore, Paths);
        Usage = new UsageRepository(protectedStore, Paths);
        BrowserActivity = new BrowserActivityRepository(protectedStore, Paths);
        Audit = new AuditRepository(protectedStore, Paths);
        Authentication = new AuthenticationService(Settings, Audit);
        RemoteSync = new RemoteSyncService(Settings, Usage, BrowserActivity, Audit);
        Monitoring = new MonitoringCoordinator(
            Settings,
            Usage,
            BrowserActivity,
            Audit,
            new WindowsActivityProbe());
    }

    public AppDataPaths Paths { get; }
    public SettingsRepository Settings { get; }
    public UsageRepository Usage { get; }
    public BrowserActivityRepository BrowserActivity { get; }
    public AuditRepository Audit { get; }
    public AuthenticationService Authentication { get; }
    public RemoteSyncService RemoteSync { get; }
    public MonitoringCoordinator Monitoring { get; }
}
