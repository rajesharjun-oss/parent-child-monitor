using ChildScreenTime.Remote.Data;
using ChildScreenTime.Remote.Models;
using Microsoft.EntityFrameworkCore;

namespace ChildScreenTime.Remote.Services;

public sealed record DashboardDevice(Guid Id, string Name, DateTimeOffset PairedUtc, DateTimeOffset? LastSeenUtc, bool Revoked);
public sealed record DashboardUsage(string Application, string Device, long Minutes, DateTimeOffset LastSeenUtc);
public sealed record DashboardSnapshot(IReadOnlyList<DashboardDevice> Devices, IReadOnlyList<DashboardUsage> Usage, long TodayMinutes);

public sealed class ParentDashboardService(ApplicationDbContext database)
{
    public async Task<DashboardSnapshot> LoadAsync(string ownerUserId, DateOnly today, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        var devices = await database.ChildDevices.AsNoTracking()
            .Where(value => value.OwnerUserId == ownerUserId)
            .OrderBy(value => value.RevokedUtc != null)
            .ThenBy(value => value.Name)
            .Select(value => new DashboardDevice(value.Id, value.Name, value.PairedUtc, value.LastSeenUtc, value.RevokedUtc != null))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var weekStart = today.AddDays(-6);
        var rows = await database.UsageAggregates.AsNoTracking()
            .Where(value => value.Device.OwnerUserId == ownerUserId && value.Device.RevokedUtc == null && value.LocalDate >= weekStart && value.LocalDate <= today)
            .GroupBy(value => new { value.Device.Name, value.ProcessName, value.DisplayName })
            .Select(group => new
            {
                group.Key.Name,
                group.Key.ProcessName,
                group.Key.DisplayName,
                Seconds = group.Sum(value => value.ActiveSeconds),
                LastSeen = group.Max(value => value.LastSeenUtc)
            })
            .OrderByDescending(value => value.Seconds)
            .Take(200)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var usage = rows.Select(value => new DashboardUsage(
            string.IsNullOrWhiteSpace(value.DisplayName) ? value.ProcessName : value.DisplayName,
            value.Name,
            (long)Math.Round(value.Seconds / 60d),
            value.LastSeen)).ToList();

        var todaySeconds = await database.UsageAggregates.AsNoTracking()
            .Where(value => value.Device.OwnerUserId == ownerUserId && value.Device.RevokedUtc == null && value.LocalDate == today)
            .SumAsync(value => value.ActiveSeconds, cancellationToken).ConfigureAwait(false);
        return new DashboardSnapshot(devices, usage, todaySeconds / 60);
    }

    public async Task<string?> RevokeAsync(string ownerUserId, Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        var device = await database.ChildDevices.SingleOrDefaultAsync(
            value => value.Id == deviceId && value.OwnerUserId == ownerUserId && value.RevokedUtc == null,
            cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return null;
        }

        device.RevokedUtc = now;
        database.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            OwnerUserId = ownerUserId,
            DeviceId = device.Id,
            EventType = "device_revoked",
            Summary = $"Device '{device.Name}' was revoked.",
            TimestampUtc = now
        });
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return device.Name;
    }

    public async Task<(string Name, int Deleted)?> DeleteUsageAsync(
        string ownerUserId,
        Guid deviceId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        var device = await database.ChildDevices.SingleOrDefaultAsync(
            value => value.Id == deviceId && value.OwnerUserId == ownerUserId,
            cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return null;
        }

        device.RevokedUtc ??= now;
        var deleted = await database.UsageAggregates
            .Where(value => value.DeviceId == device.Id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        database.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            OwnerUserId = ownerUserId,
            DeviceId = device.Id,
            EventType = "remote_usage_deleted",
            Summary = $"Remote usage history for '{device.Name}' was permanently deleted and the device was revoked.",
            TimestampUtc = now
        });
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (device.Name, deleted);
    }
}
