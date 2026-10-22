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
        Audit = new AuditRepository(protectedStore, Paths);
        Authentication = new AuthenticationService(Settings, Audit);
        Monitoring = new MonitoringCoordinator(
            Settings,
            Usage,
            Audit,
            new WindowsActivityProbe());
    }

    public AppDataPaths Paths { get; }
    public SettingsRepository Settings { get; }
    public UsageRepository Usage { get; }
    public AuditRepository Audit { get; }
    public AuthenticationService Authentication { get; }
    public MonitoringCoordinator Monitoring { get; }
}
