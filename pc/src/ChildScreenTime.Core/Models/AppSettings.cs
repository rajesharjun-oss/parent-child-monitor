namespace ChildScreenTime.Core.Models;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public bool IsConfigured { get; set; }
    public bool MonitoringEnabled { get; set; } = true;
    public int DailyLimitMinutes { get; set; } = 120;
    public bool BedtimeEnabled { get; set; } = true;
    public TimeOnly BedtimeStart { get; set; } = new(21, 0);
    public TimeOnly BedtimeEnd { get; set; } = new(7, 0);
    public int RetentionDays { get; set; } = 30;
    public string ParentPasswordHash { get; set; } = string.Empty;
    public int FailedAuthenticationCount { get; set; }
    public DateTimeOffset? AuthenticationLockedUntilUtc { get; set; }
    public DateTimeOffset? TemporaryOverrideUntilUtc { get; set; }
    public List<AppRule> AppRules { get; set; } = [];
    public RemoteSyncSettings RemoteSync { get; set; } = new();
}
