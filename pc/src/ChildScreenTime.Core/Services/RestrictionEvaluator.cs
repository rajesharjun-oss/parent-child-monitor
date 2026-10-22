using ChildScreenTime.Core.Models;

namespace ChildScreenTime.Core.Services;

public static class RestrictionEvaluator
{
    public static RestrictionDecision Evaluate(
        AppSettings settings,
        DateTimeOffset now,
        string foregroundProcessName,
        long totalActiveSecondsToday,
        long foregroundAppActiveSecondsToday)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var globalRemaining = Math.Max(
            0,
            settings.DailyLimitMinutes - (int)Math.Ceiling(totalActiveSecondsToday / 60d));

        if (!settings.MonitoringEnabled)
        {
            return RestrictionDecision.Allowed(globalRemaining);
        }

        if (settings.TemporaryOverrideUntilUtc is { } overrideUntil && overrideUntil > now.ToUniversalTime())
        {
            return RestrictionDecision.Allowed(globalRemaining);
        }

        if (settings.BedtimeEnabled && IsWithinBedtime(now.TimeOfDay, settings.BedtimeStart, settings.BedtimeEnd))
        {
            return new(true, RestrictionReason.Bedtime, "This PC is unavailable during the configured bedtime.", 0);
        }

        if (totalActiveSecondsToday >= settings.DailyLimitMinutes * 60L)
        {
            return new(true, RestrictionReason.DailyLimit, "Today's screen-time limit has been reached.", 0);
        }

        var normalizedProcess = NormalizeProcessName(foregroundProcessName);
        var rule = settings.AppRules.FirstOrDefault(candidate =>
            string.Equals(NormalizeProcessName(candidate.ProcessName), normalizedProcess, StringComparison.OrdinalIgnoreCase));

        if (rule is null)
        {
            return RestrictionDecision.Allowed(globalRemaining);
        }

        if (rule.AlwaysBlocked)
        {
            return new(true, RestrictionReason.AppBlocked, $"{SafeDisplayName(rule)} is blocked by a parent.", globalRemaining);
        }

        if (rule.DailyLimitMinutes is { } appLimit && foregroundAppActiveSecondsToday >= appLimit * 60L)
        {
            return new(true, RestrictionReason.AppLimit, $"Today's limit for {SafeDisplayName(rule)} has been reached.", globalRemaining);
        }

        return RestrictionDecision.Allowed(globalRemaining);
    }

    public static bool IsWithinBedtime(TimeSpan now, TimeOnly start, TimeOnly end)
    {
        var startTime = start.ToTimeSpan();
        var endTime = end.ToTimeSpan();

        if (startTime == endTime)
        {
            return false;
        }

        return startTime < endTime
            ? now >= startTime && now < endTime
            : now >= startTime || now < endTime;
    }

    public static string NormalizeProcessName(string processName)
    {
        var trimmed = (processName ?? string.Empty).Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }

    private static string SafeDisplayName(AppRule rule) =>
        string.IsNullOrWhiteSpace(rule.DisplayName) ? rule.ProcessName : rule.DisplayName;
}
