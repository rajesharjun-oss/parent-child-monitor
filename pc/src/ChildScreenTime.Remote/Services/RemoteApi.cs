using System.Data;
using System.Text.RegularExpressions;
using ChildScreenTime.Core.Models;
using ChildScreenTime.Remote.Data;
using ChildScreenTime.Remote.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace ChildScreenTime.Remote.Services;

public static partial class RemoteApi
{
    [GeneratedRegex("^[A-Za-z0-9._-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex ProcessNamePattern();

    public static IEndpointRouteBuilder MapRemoteApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1").RequireRateLimiting("api");
        api.MapPost("/devices/pair", PairDeviceAsync).RequireRateLimiting("pairing");
        api.MapPost("/usage/batch", UploadUsageAsync).RequireRateLimiting("device");
        api.MapPost("/browser-activity/batch", UploadBrowserActivityAsync).RequireRateLimiting("device");
        return endpoints;
    }

    private static async Task<Results<Ok<PairDeviceResponse>, BadRequest, UnauthorizedHttpResult>> PairDeviceAsync(
        PairDeviceRequest request,
        ApplicationDbContext database,
        CredentialService credentials,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var code = request.PairingCode?.Trim();
        var name = request.DeviceName?.Trim();
        if (code is null || code.Length is < 20 or > 200 || name is null || name.Length is < 1 or > 100 || name.Any(char.IsControl))
        {
            return TypedResults.BadRequest();
        }

        var now = clock.GetUtcNow();
        var codeHash = credentials.Hash(code);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        var pairing = await database.PairingCodes.SingleOrDefaultAsync(
            value => value.CodeHash == codeHash && value.UsedUtc == null && value.ExpiresUtc > now,
            cancellationToken).ConfigureAwait(false);
        if (pairing is null)
        {
            return TypedResults.Unauthorized();
        }

        var token = credentials.CreateDeviceToken();
        var device = new ChildDevice
        {
            Id = Guid.NewGuid(),
            OwnerUserId = pairing.OwnerUserId,
            Name = name,
            TokenHash = credentials.Hash(token),
            PairedUtc = now,
            LastSeenUtc = now
        };
        pairing.UsedUtc = now;
        database.ChildDevices.Add(device);
        database.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            OwnerUserId = pairing.OwnerUserId,
            DeviceId = device.Id,
            EventType = "device_paired",
            Summary = $"Device '{name}' was paired.",
            TimestampUtc = now
        });
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new PairDeviceResponse(device.Id, token, now));
    }

    private static async Task<Results<Ok<UsageUploadResponse>, BadRequest, UnauthorizedHttpResult>> UploadUsageAsync(
        UsageUploadRequest request,
        HttpRequest httpRequest,
        DeviceAuthenticator authenticator,
        ApplicationDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var device = await authenticator.AuthenticateAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return TypedResults.Unauthorized();
        }

        if (request.Records is null || request.Records.Count > 500)
        {
            return TypedResults.BadRequest();
        }

        var now = clock.GetUtcNow();
        var oldestDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-366));
        var newestDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(1));
        foreach (var record in request.Records)
        {
            if (record is null || string.IsNullOrWhiteSpace(record.ProcessName) || !ProcessNamePattern().IsMatch(record.ProcessName) ||
                record.DisplayName is null || record.DisplayName.Length > 200 || record.DisplayName.Any(char.IsControl) ||
                record.ActiveSeconds is < 0 or > 86400 || record.LocalDate < oldestDate || record.LocalDate > newestDate ||
                record.LastSeenUtc > now.AddMinutes(5) || record.LastSeenUtc < now.AddDays(-367))
            {
                return TypedResults.BadRequest();
            }
        }

        foreach (var incoming in request.Records)
        {
            var processName = incoming.ProcessName.ToLowerInvariant();
            var existing = await database.UsageAggregates.SingleOrDefaultAsync(
                value => value.DeviceId == device.Id && value.LocalDate == incoming.LocalDate && value.ProcessName == processName,
                cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                database.UsageAggregates.Add(new UsageAggregate
                {
                    DeviceId = device.Id,
                    LocalDate = incoming.LocalDate,
                    ProcessName = processName,
                    DisplayName = incoming.DisplayName.Trim(),
                    ActiveSeconds = incoming.ActiveSeconds,
                    LastSeenUtc = incoming.LastSeenUtc,
                    UpdatedUtc = now
                });
            }
            else
            {
                existing.ActiveSeconds = Math.Max(existing.ActiveSeconds, incoming.ActiveSeconds);
                existing.LastSeenUtc = incoming.LastSeenUtc > existing.LastSeenUtc ? incoming.LastSeenUtc : existing.LastSeenUtc;
                existing.DisplayName = incoming.DisplayName.Trim();
                existing.UpdatedUtc = now;
            }
        }

        device.LastSeenUtc = now;
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new UsageUploadResponse(request.Records.Count, now));
    }
    private static async Task<Results<Ok<BrowserActivityUploadResponse>, BadRequest, UnauthorizedHttpResult>> UploadBrowserActivityAsync(
        BrowserActivityUploadRequest request,
        HttpRequest httpRequest,
        DeviceAuthenticator authenticator,
        ApplicationDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var device = await authenticator.AuthenticateAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return TypedResults.Unauthorized();
        }
        if (request.Records is null || request.Records.Count > 500)
        {
            return TypedResults.BadRequest();
        }

        var now = clock.GetUtcNow();
        var oldestDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-366));
        var newestDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(1));
        foreach (var record in request.Records)
        {
            if (record is null ||
                !BrowserActivityPolicy.IsValidVideoId(record.VideoId) ||
                !BrowserActivityPolicy.IsSafeText(record.Title, 1, 200) ||
                !BrowserActivityPolicy.IsSafeText(record.Channel, 0, 120) ||
                record.WatchedSeconds is < 0 or > 86400 ||
                record.LocalDate < oldestDate || record.LocalDate > newestDate ||
                record.LastSeenUtc > now.AddMinutes(5) || record.LastSeenUtc < now.AddDays(-367))
            {
                return TypedResults.BadRequest();
            }
        }

        foreach (var incoming in request.Records)
        {
            var existing = await database.BrowserActivityAggregates.SingleOrDefaultAsync(
                value => value.DeviceId == device.Id &&
                         value.LocalDate == incoming.LocalDate &&
                         value.VideoId == incoming.VideoId,
                cancellationToken).ConfigureAwait(false);

            if (existing is null)
            {
                database.BrowserActivityAggregates.Add(new BrowserActivityAggregate
                {
                    DeviceId = device.Id,
                    LocalDate = incoming.LocalDate,
                    VideoId = incoming.VideoId,
                    Title = incoming.Title.Trim(),
                    Channel = incoming.Channel.Trim(),
                    WatchedSeconds = incoming.WatchedSeconds,
                    LastSeenUtc = incoming.LastSeenUtc,
                    UpdatedUtc = now
                });
            }
            else
            {
                existing.WatchedSeconds = Math.Max(existing.WatchedSeconds, incoming.WatchedSeconds);
                existing.LastSeenUtc = incoming.LastSeenUtc > existing.LastSeenUtc ? incoming.LastSeenUtc : existing.LastSeenUtc;
                existing.Title = incoming.Title.Trim();
                existing.Channel = incoming.Channel.Trim();
                existing.UpdatedUtc = now;
            }
        }

        device.LastSeenUtc = now;
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new BrowserActivityUploadResponse(request.Records.Count, now));
    }
}
