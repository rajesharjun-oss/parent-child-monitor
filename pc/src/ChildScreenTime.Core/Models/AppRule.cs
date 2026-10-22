namespace ChildScreenTime.Core.Models;

public sealed class AppRule
{
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int? DailyLimitMinutes { get; set; }
    public bool AlwaysBlocked { get; set; }
}
