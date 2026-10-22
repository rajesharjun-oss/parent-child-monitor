using ChildScreenTime.Core.Models;
using ChildScreenTime.Core.Services;

namespace ChildScreenTime.Windows.Services;

public sealed class SettingsRepository
{
    private readonly object gate = new();
    private readonly DpapiFileStore store;
    private readonly string path;
    private AppSettings settings;

    public SettingsRepository(DpapiFileStore store, AppDataPaths paths)
    {
        this.store = store;
        path = paths.SettingsFile;
        settings = store.Load(path, () => new AppSettings());
        EnsureValid(settings);
    }

    public AppSettings GetSnapshot()
    {
        lock (gate)
        {
            return Clone(settings);
        }
    }

    public AppSettings Update(Action<AppSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        lock (gate)
        {
            var candidate = Clone(settings);
            update(candidate);
            EnsureValid(candidate);
            store.Save(path, candidate);
            settings = candidate;
            return Clone(settings);
        }
    }

    private static void EnsureValid(AppSettings candidate)
    {
        var errors = SettingsValidator.Validate(candidate);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(" ", errors));
        }
    }

    private static AppSettings Clone(AppSettings source) => new()
    {
        SchemaVersion = source.SchemaVersion,
        IsConfigured = source.IsConfigured,
        MonitoringEnabled = source.MonitoringEnabled,
        DailyLimitMinutes = source.DailyLimitMinutes,
        BedtimeEnabled = source.BedtimeEnabled,
        BedtimeStart = source.BedtimeStart,
        BedtimeEnd = source.BedtimeEnd,
        RetentionDays = source.RetentionDays,
        ParentPasswordHash = source.ParentPasswordHash,
        FailedAuthenticationCount = source.FailedAuthenticationCount,
        AuthenticationLockedUntilUtc = source.AuthenticationLockedUntilUtc,
        TemporaryOverrideUntilUtc = source.TemporaryOverrideUntilUtc,
        AppRules = source.AppRules.Select(rule => new AppRule
        {
            ProcessName = rule.ProcessName,
            DisplayName = rule.DisplayName,
            DailyLimitMinutes = rule.DailyLimitMinutes,
            AlwaysBlocked = rule.AlwaysBlocked
        }).ToList()
    };
}
