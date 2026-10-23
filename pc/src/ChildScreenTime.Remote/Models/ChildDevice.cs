namespace ChildScreenTime.Remote.Models;

public sealed class ChildDevice
{
    public Guid Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public ApplicationUser Owner { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset PairedUtc { get; set; }
    public DateTimeOffset? LastSeenUtc { get; set; }
    public DateTimeOffset? RevokedUtc { get; set; }
    public List<UsageAggregate> Usage { get; set; } = [];
    public List<BrowserActivityAggregate> BrowserActivity { get; set; } = [];
}
