namespace ChildScreenTime.Core.Models;

public sealed class UsageRecord
{
    public DateOnly LocalDate { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public long ActiveSeconds { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
}
