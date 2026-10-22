namespace ChildScreenTime.Remote.Models;

public sealed class SecurityAuditEvent
{
    public long Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public ApplicationUser Owner { get; set; } = null!;
    public Guid? DeviceId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
}
