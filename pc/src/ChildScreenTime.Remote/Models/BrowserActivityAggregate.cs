namespace ChildScreenTime.Remote.Models;

public sealed class BrowserActivityAggregate
{
    public long Id { get; set; }
    public Guid DeviceId { get; set; }
    public ChildDevice Device { get; set; } = null!;
    public DateOnly LocalDate { get; set; }
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public long WatchedSeconds { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
