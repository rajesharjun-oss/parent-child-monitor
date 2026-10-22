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
public sealed class LoginModel(SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public sealed class LoginInput
    {
        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 12)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Dashboard/Index");
        }
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

        var result = await signInManager.PasswordSignInAsync(Input.Email.Trim(), Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return LocalRedirect(ReturnUrl!);
        }
        if (result.RequiresTwoFactor)
        {
            return RedirectToPage("/Account/TwoFactor", new { returnUrl = ReturnUrl, rememberMe = Input.RememberMe });
        }

        ModelState.AddModelError(string.Empty, "The email or password is incorrect, or the account is temporarily locked.");
        return Page();
    }
}
