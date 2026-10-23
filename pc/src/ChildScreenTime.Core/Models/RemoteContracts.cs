namespace ChildScreenTime.Core.Models;

public sealed record PairDeviceRequest(string PairingCode, string DeviceName);

public sealed record PairDeviceResponse(Guid DeviceId, string DeviceToken, DateTimeOffset PairedAtUtc);

public sealed record UsageUploadRequest(IReadOnlyList<UsageUploadItem> Records);

public sealed record UsageUploadItem(
    DateOnly LocalDate,
    string ProcessName,
    string DisplayName,
    long ActiveSeconds,
    DateTimeOffset LastSeenUtc);

public sealed record UsageUploadResponse(int AcceptedRecords, DateTimeOffset ReceivedAtUtc);

public sealed record BrowserActivityUploadRequest(IReadOnlyList<BrowserActivityUploadItem> Records);

public sealed record BrowserActivityUploadItem(
    DateOnly LocalDate,
    string VideoId,
    string Title,
    string Channel,
    long WatchedSeconds,
    DateTimeOffset LastSeenUtc);

public sealed record BrowserActivityUploadResponse(int AcceptedRecords, DateTimeOffset ReceivedAtUtc);
