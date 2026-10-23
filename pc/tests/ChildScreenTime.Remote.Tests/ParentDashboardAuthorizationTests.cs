using ChildScreenTime.Remote.Data;
using ChildScreenTime.Remote.Models;
using ChildScreenTime.Remote.Services;
using Microsoft.EntityFrameworkCore;

namespace ChildScreenTime.Remote.Tests;

[TestClass]
public sealed class ParentDashboardAuthorizationTests
{
    [TestMethod]
    public async Task DashboardReturnsOnlyOwnedDevicesAndUsage()
    {
        await using var database = CreateDatabase();
        var today = new DateOnly(2026, 10, 22);
        var ownerA = User("owner-a");
        var ownerB = User("owner-b");
        var deviceA = Device(ownerA, "Child A");
        var deviceB = Device(ownerB, "Child B");
        database.AddRange(ownerA, ownerB, deviceA, deviceB);
        database.UsageAggregates.AddRange(
            Usage(deviceA, today, "browser.exe", 600),
            Usage(deviceB, today, "private.exe", 3600));
        await database.SaveChangesAsync();

        var snapshot = await new ParentDashboardService(database).LoadAsync(ownerA.Id, today);

        Assert.HasCount(1, snapshot.Devices);
        Assert.AreEqual(deviceA.Id, snapshot.Devices[0].Id);
        Assert.HasCount(1, snapshot.Usage);
        Assert.AreEqual("Browser", snapshot.Usage[0].Application);
        Assert.AreEqual(10, snapshot.TodayMinutes);
        Assert.IsEmpty(snapshot.BrowserActivity);
    }

    [TestMethod]
    public async Task DashboardReturnsOnlyOwnedYouTubeActivity()
    {
        await using var database = CreateDatabase();
        var today = new DateOnly(2026, 10, 23);
        var ownerA = User("owner-a");
        var ownerB = User("owner-b");
        var deviceA = Device(ownerA, "Child A");
        var deviceB = Device(ownerB, "Child B");
        database.AddRange(ownerA, ownerB, deviceA, deviceB);
        database.BrowserActivityAggregates.AddRange(
            BrowserActivity(deviceA, today, "dQw4w9WgXcQ", "Owned video", 600),
            BrowserActivity(deviceB, today, "9bZkp7q19f0", "Private video", 3600));
        await database.SaveChangesAsync();

        var snapshot = await new ParentDashboardService(database).LoadAsync(ownerA.Id, today);

        Assert.HasCount(1, snapshot.BrowserActivity);
        Assert.AreEqual("Owned video", snapshot.BrowserActivity[0].Title);
        Assert.AreEqual(10, snapshot.TodayYouTubeMinutes);
    }

    [TestMethod]
    public async Task ParentCannotRevokeAnotherParentsDevice()
    {
        await using var database = CreateDatabase();
        var ownerA = User("owner-a");
        var ownerB = User("owner-b");
        var deviceB = Device(ownerB, "Child B");
        database.AddRange(ownerA, ownerB, deviceB);
        await database.SaveChangesAsync();

        var result = await new ParentDashboardService(database).RevokeAsync(ownerA.Id, deviceB.Id, DateTimeOffset.UtcNow);

        Assert.IsNull(result);
        Assert.IsNull((await database.ChildDevices.FindAsync(deviceB.Id))!.RevokedUtc);
    }

    [TestMethod]
    public async Task ParentCannotDeleteAnotherParentsUsage()
    {
        await using var database = CreateDatabase();
        var ownerA = User("owner-a");
        var ownerB = User("owner-b");
        var deviceB = Device(ownerB, "Child B");
        database.AddRange(ownerA, ownerB, deviceB);
        database.UsageAggregates.Add(Usage(deviceB, new DateOnly(2026, 10, 22), "private.exe", 1200));
        await database.SaveChangesAsync();

        var result = await new ParentDashboardService(database).DeleteUsageAsync(ownerA.Id, deviceB.Id, DateTimeOffset.UtcNow);

        Assert.IsNull(result);
        Assert.AreEqual(1, await database.UsageAggregates.CountAsync());
    }

    private static ApplicationDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ApplicationUser User(string id) => new() { Id = id, UserName = $"{id}@example.test", Email = $"{id}@example.test", DisplayName = id };

    private static ChildDevice Device(ApplicationUser owner, string name) => new()
    {
        Id = Guid.NewGuid(),
        Owner = owner,
        OwnerUserId = owner.Id,
        Name = name,
        TokenHash = new string('A', 64),
        PairedUtc = DateTimeOffset.UtcNow
    };

    private static BrowserActivityAggregate BrowserActivity(ChildDevice device, DateOnly date, string videoId, string title, long seconds) => new()
    {
        Device = device,
        DeviceId = device.Id,
        LocalDate = date,
        VideoId = videoId,
        Title = title,
        Channel = "Example channel",
        WatchedSeconds = seconds,
        LastSeenUtc = DateTimeOffset.UtcNow,
        UpdatedUtc = DateTimeOffset.UtcNow
    };

    private static UsageAggregate Usage(ChildDevice device, DateOnly date, string process, long seconds) => new()
    {
        Device = device,
        DeviceId = device.Id,
        LocalDate = date,
        ProcessName = process,
        DisplayName = "Browser",
        ActiveSeconds = seconds,
        LastSeenUtc = DateTimeOffset.UtcNow,
        UpdatedUtc = DateTimeOffset.UtcNow
    };
}
