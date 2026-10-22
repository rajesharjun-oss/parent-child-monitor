namespace ChildScreenTime.Core.Models;

public enum RestrictionReason
{
    None,
    Bedtime,
    DailyLimit,
    AppBlocked,
    AppLimit
}

public sealed record RestrictionDecision(
    bool IsBlocked,
    RestrictionReason Reason,
    string Message,
    int RemainingMinutes)
{
    public static RestrictionDecision Allowed(int remainingMinutes) =>
        new(false, RestrictionReason.None, "Available", Math.Max(0, remainingMinutes));
}
