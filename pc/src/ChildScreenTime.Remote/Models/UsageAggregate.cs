namespace ChildScreenTime.Remote.Models;

public sealed class UsageAggregate
{
    public long Id { get; set; }
    public Guid DeviceId { get; set; }
    public ChildDevice Device { get; set; } = null!;
    public DateOnly LocalDate { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public long ActiveSeconds { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
