using ChildScreenTime.Core.Models;
using ChildScreenTime.Core.Services;

namespace ChildScreenTime.Core.Tests;

[TestClass]
public sealed class RestrictionEvaluatorTests
{
    [TestMethod]
    public void BlocksAtGlobalDailyLimit()
    {
        var settings = ValidSettings();
        var result = RestrictionEvaluator.Evaluate(settings, LocalTime(12), "game", 120 * 60, 0);

        Assert.IsTrue(result.IsBlocked);
        Assert.AreEqual(RestrictionReason.DailyLimit, result.Reason);
    }

    [TestMethod]
    public void BlocksDuringOvernightBedtime()
    {
        var settings = ValidSettings();
        var result = RestrictionEvaluator.Evaluate(settings, LocalTime(23), "browser", 0, 0);

        Assert.IsTrue(result.IsBlocked);
        Assert.AreEqual(RestrictionReason.Bedtime, result.Reason);
    }

    [TestMethod]
    public void AllowsOutsideOvernightBedtime()
    {
        var settings = ValidSettings();
        var result = RestrictionEvaluator.Evaluate(settings, LocalTime(12), "browser", 60, 60);

        Assert.IsFalse(result.IsBlocked);
    }

    [TestMethod]
    public void BlocksMatchingApplicationAfterItsLimit()
    {
        var settings = ValidSettings();
        settings.AppRules.Add(new AppRule { ProcessName = "game.exe", DailyLimitMinutes = 30 });

        var result = RestrictionEvaluator.Evaluate(settings, LocalTime(12), "GAME", 40 * 60, 30 * 60);

        Assert.IsTrue(result.IsBlocked);
        Assert.AreEqual(RestrictionReason.AppLimit, result.Reason);
    }

    [TestMethod]
    public void ActiveOverrideTemporarilyAllowsAccess()
    {
        var now = LocalTime(23);
        var settings = ValidSettings();
        settings.TemporaryOverrideUntilUtc = now.ToUniversalTime().AddMinutes(15);

        var result = RestrictionEvaluator.Evaluate(settings, now, "game", 300 * 60, 0);

        Assert.IsFalse(result.IsBlocked);
    }

    private static AppSettings ValidSettings() => new()
    {
        IsConfigured = true,
        ParentPasswordHash = "test-hash",
        DailyLimitMinutes = 120,
        BedtimeEnabled = true,
        BedtimeStart = new TimeOnly(21, 0),
        BedtimeEnd = new TimeOnly(7, 0)
    };

    private static DateTimeOffset LocalTime(int hour) =>
        new(2026, 10, 22, hour, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 22)));
}
