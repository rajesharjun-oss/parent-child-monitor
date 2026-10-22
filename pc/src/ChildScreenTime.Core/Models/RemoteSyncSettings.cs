namespace ChildScreenTime.Core.Models;

public sealed class RemoteSyncSettings
{
    public string ServerBaseUrl { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceToken { get; set; } = string.Empty;
    public string DeviceName { get; set; } = Environment.MachineName;
    public DateTimeOffset? LastSuccessfulSyncUtc { get; set; }

    public bool IsPaired =>
        !string.IsNullOrWhiteSpace(ServerBaseUrl) &&
        !string.IsNullOrWhiteSpace(DeviceId) &&
        !string.IsNullOrWhiteSpace(DeviceToken);
}
