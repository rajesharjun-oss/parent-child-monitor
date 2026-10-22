using System.ComponentModel.DataAnnotations;
using ChildScreenTime.Remote.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace ChildScreenTime.Remote.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("auth")]
public sealed class TwoFactorModel(SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty, Required, StringLength(30, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    [BindProperty(SupportsGet = true)] public bool RememberMe { get; set; }
    [BindProperty] public bool RememberMachine { get; set; }

    public IActionResult OnGet()
    {
        ReturnUrl = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Page("/Dashboard/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ReturnUrl = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Page("/Dashboard/Index");
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var normalized = Code.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        var result = normalized.Length == 6 && normalized.All(char.IsDigit)
            ? await signInManager.TwoFactorAuthenticatorSignInAsync(normalized, RememberMe, RememberMachine)
            : await signInManager.TwoFactorRecoveryCodeSignInAsync(Code.Trim());
        if (result.Succeeded)
        {
            return LocalRedirect(ReturnUrl!);
        }

        ModelState.AddModelError(string.Empty, "The security code is invalid or the account is temporarily locked.");
        return Page();
    }
}
