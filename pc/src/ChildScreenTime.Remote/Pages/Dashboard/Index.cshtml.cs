using ChildScreenTime.Remote.Configuration;
using ChildScreenTime.Remote.Data;
using ChildScreenTime.Remote.Models;
using ChildScreenTime.Remote.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChildScreenTime.Remote.Pages.Dashboard;

[Authorize]
public sealed class DashboardModel(
    ApplicationDbContext database,
    UserManager<ApplicationUser> userManager,
    CredentialService credentials,
    ParentDashboardService dashboard,
    IOptions<RemoteOptions> options,
    TimeProvider clock) : PageModel
{
    public List<DeviceView> Devices { get; private set; } = [];
    public List<UsageView> WeeklyUsage { get; private set; } = [];
    public long TodayMinutes { get; private set; }
    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? NewPairingCode { get; set; }
    [TempData] public string? PairingCodeExpiresLocal { get; set; }

    public sealed record DeviceView(Guid Id, string Name, DateTimeOffset PairedLocal, DateTimeOffset? LastSeenLocal, bool Revoked);
    public sealed record UsageView(string Application, string Device, long Minutes, DateTimeOffset LastSeenLocal);

    public async Task OnGetAsync()
    {
        var userId = userManager.GetUserId(User) ?? throw new InvalidOperationException("Authenticated user has no identifier.");
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var snapshot = await dashboard.LoadAsync(userId, today);
        Devices = snapshot.Devices.Select(value => new DeviceView(
            value.Id, value.Name, value.PairedUtc.ToLocalTime(), value.LastSeenUtc?.ToLocalTime(), value.Revoked)).ToList();
        WeeklyUsage = snapshot.Usage.Select(value => new UsageView(
            value.Application, value.Device, value.Minutes, value.LastSeenUtc.ToLocalTime())).ToList();
        TodayMinutes = snapshot.TodayMinutes;
    }

    public async Task<IActionResult> OnPostCreatePairingCodeAsync(string currentPassword)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null || !await userManager.CheckPasswordAsync(user, currentPassword ?? string.Empty))
        {
            StatusMessage = "The password could not be verified.";
            return RedirectToPage();
        }

        var now = clock.GetUtcNow();
        var code = credentials.CreatePairingCode();
        var expires = now.AddMinutes(options.Value.PairingCodeLifetimeMinutes);
        database.PairingCodes.Add(new PairingCode
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            CodeHash = credentials.Hash(code),
            CreatedUtc = now,
            ExpiresUtc = expires
        });
        database.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            OwnerUserId = user.Id,
            EventType = "pairing_code_created",
            Summary = "A one-time device pairing code was created.",
            TimestampUtc = now
        });
        await database.SaveChangesAsync();
        NewPairingCode = code;
        PairingCodeExpiresLocal = expires.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);
        StatusMessage = "Pairing code created. It can be used once.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevokeAsync(Guid deviceId, string currentPassword)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null || !await userManager.CheckPasswordAsync(user, currentPassword ?? string.Empty))
        {
            StatusMessage = "The password could not be verified.";
            return RedirectToPage();
        }

        var name = await dashboard.RevokeAsync(user.Id, deviceId, clock.GetUtcNow());
        if (name is null)
        {
            StatusMessage = "The device was not found or is already revoked.";
            return RedirectToPage();
        }

        StatusMessage = $"{name} can no longer upload usage.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteUsageAsync(Guid deviceId, string currentPassword, bool confirmed)
    {
        var user = await userManager.GetUserAsync(User);
        if (!confirmed || user is null || !await userManager.CheckPasswordAsync(user, currentPassword ?? string.Empty))
        {
            StatusMessage = "The deletion confirmation or password could not be verified.";
            return RedirectToPage();
        }

        var result = await dashboard.DeleteUsageAsync(user.Id, deviceId, clock.GetUtcNow());
        StatusMessage = result is null
            ? "The device was not found."
            : $"Deleted {result.Value.Deleted} synchronized records for {result.Value.Name}.";
        return RedirectToPage();
    }
}
