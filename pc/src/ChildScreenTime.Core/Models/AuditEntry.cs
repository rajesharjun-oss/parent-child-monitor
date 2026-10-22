namespace ChildScreenTime.Core.Models;

public sealed class AuditEntry
{
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string EventType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}
