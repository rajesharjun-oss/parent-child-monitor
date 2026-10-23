using System.Text.RegularExpressions;

namespace ChildScreenTime.Core.Models;

public sealed class BrowserPlaybackEvent
{
    public DateOnly LocalDate { get; set; }
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public int WatchedSeconds { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
}

public sealed class BrowserActivityRecord
{
    public DateOnly LocalDate { get; set; }
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public long WatchedSeconds { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
}

public static partial class BrowserActivityPolicy
{
    [GeneratedRegex("^[A-Za-z0-9_-]{11}$", RegexOptions.CultureInvariant)]
    private static partial Regex YouTubeVideoIdPattern();

    public static bool IsValidVideoId(string? value) =>
        value is not null && YouTubeVideoIdPattern().IsMatch(value);

    public static bool IsSafeText(string? value, int minimumLength, int maximumLength) =>
        value is not null &&
        value.Length >= minimumLength &&
        value.Length <= maximumLength &&
        !value.Any(char.IsControl);

    public static bool IsValidPlaybackEvent(BrowserPlaybackEvent? value) =>
        value is not null &&
        value.LocalDate.Year >= 2000 &&
        IsValidVideoId(value.VideoId) &&
        IsSafeText(value.Title, 1, 200) &&
        IsSafeText(value.Channel, 0, 120) &&
        value.WatchedSeconds is >= 1 and <= 30 &&
        value.LastSeenUtc != default;
}
