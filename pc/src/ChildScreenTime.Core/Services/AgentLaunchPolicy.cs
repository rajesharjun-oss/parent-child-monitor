namespace ChildScreenTime.Core.Services;

public static class AgentLaunchPolicy
{
    public const uint NoActiveSession = uint.MaxValue;

    public static bool ShouldLaunch(uint activeSessionId, IEnumerable<uint> runningSessionIds)
    {
        ArgumentNullException.ThrowIfNull(runningSessionIds);

        return activeSessionId != NoActiveSession &&
               !runningSessionIds.Contains(activeSessionId);
    }
}
