namespace ChildScreenTime.Windows.Services;

public sealed class AppDataPaths
{
    public AppDataPaths(string? rootOverride = null)
    {
        Root = rootOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChildScreenTime");
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }
    public string SettingsFile => Path.Combine(Root, "settings.protected");
    public string UsageFile => Path.Combine(Root, "usage.protected");
    public string BrowserUsageFile => Path.Combine(Root, "browser-usage.protected");
    public string BrowserInboxDirectory => Path.Combine(Root, "browser-inbox");
    public string AuditFile => Path.Combine(Root, "audit.protected");
    public string DiagnosticLog => Path.Combine(Root, "diagnostics.log");
}
