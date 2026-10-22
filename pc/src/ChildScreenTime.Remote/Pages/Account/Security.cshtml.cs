using ChildScreenTime.Remote.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChildScreenTime.Remote.Pages.Account;

[Authorize]
public sealed class SecurityModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager) : PageModel
{
    public bool IsEnabled { get; private set; }
    public string SharedKey { get; private set; } = string.Empty;
    public string? StatusMessage { get; private set; }
    public IReadOnlyList<string> RecoveryCodes { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostEnableAsync(string currentPassword, string code)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        if (!await userManager.CheckPasswordAsync(user, currentPassword ?? string.Empty))
        {
            StatusMessage = "The password could not be verified.";
            await LoadAsync(user);
            return Page();
        }

        var normalized = (code ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        if (!await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, normalized))
        {
            StatusMessage = "The authenticator code is invalid.";
            await LoadAsync(user);
            return Page();
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        RecoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8))?.ToArray() ?? [];
        await userManager.UpdateSecurityStampAsync(user);
        await signInManager.RefreshSignInAsync(user);
        StatusMessage = "Two-factor authentication is enabled.";
        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostDisableAsync(string currentPassword)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        if (!await userManager.CheckPasswordAsync(user, currentPassword ?? string.Empty))
        {
            StatusMessage = "The password could not be verified.";
            await LoadAsync(user);
            return Page();
        }

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        await userManager.UpdateSecurityStampAsync(user);
        await signInManager.RefreshSignInAsync(user);
        StatusMessage = "Two-factor authentication is disabled.";
        await LoadAsync(user);
        return Page();
    }

    private async Task LoadAsync(ApplicationUser user)
    {
        IsEnabled = await userManager.GetTwoFactorEnabledAsync(user);
        if (IsEnabled) return;
        SharedKey = await userManager.GetAuthenticatorKeyAsync(user) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(SharedKey))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            SharedKey = await userManager.GetAuthenticatorKeyAsync(user) ?? throw new InvalidOperationException("Could not create an authenticator key.");
        }
    }
}
