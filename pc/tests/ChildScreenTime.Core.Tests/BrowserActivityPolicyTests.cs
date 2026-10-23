using ChildScreenTime.Core.Models;

namespace ChildScreenTime.Core.Tests;

[TestClass]
public sealed class BrowserActivityPolicyTests
{
    [TestMethod]
    public void AcceptsBoundedYouTubePlaybackEvent()
    {
        var value = new BrowserPlaybackEvent
        {
            LocalDate = new DateOnly(2026, 10, 23),
            VideoId = "dQw4w9WgXcQ",
            Title = "Example video",
            Channel = "Example channel",
            WatchedSeconds = 10,
            LastSeenUtc = DateTimeOffset.UtcNow
        };

        Assert.IsTrue(BrowserActivityPolicy.IsValidPlaybackEvent(value));
    }

    [TestMethod]
    public void RejectsInvalidVideoIdsAndControlCharacters()
    {
        Assert.IsFalse(BrowserActivityPolicy.IsValidVideoId("not-a-video"));
        Assert.IsFalse(BrowserActivityPolicy.IsSafeText("bad\nvalue", 1, 200));
    }

    [TestMethod]
    public void RejectsOversizedPlaybackDelta()
    {
        var value = new BrowserPlaybackEvent
        {
            LocalDate = new DateOnly(2026, 10, 23),
            VideoId = "dQw4w9WgXcQ",
            Title = "Example video",
            Channel = string.Empty,
            WatchedSeconds = 31,
            LastSeenUtc = DateTimeOffset.UtcNow
        };

        Assert.IsFalse(BrowserActivityPolicy.IsValidPlaybackEvent(value));
    }
}
