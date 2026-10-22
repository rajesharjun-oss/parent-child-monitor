using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using ChildScreenTime.Remote.Configuration;
using ChildScreenTime.Remote.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ChildScreenTime.Remote.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("auth")]
public sealed class RegisterModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IOptions<RemoteOptions> options) : PageModel
{
    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    public sealed class RegisterInput
    {
        [Required, StringLength(100, MinimumLength = 2)]
        public string DisplayName { get; set; } = string.Empty;
        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; } = string.Empty;
        [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 12)]
        public string Password { get; set; } = string.Empty;
        [Required, DataType(DataType.Password), Compare(nameof(Password))]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
        [Required, DataType(DataType.Password)]
        [Display(Name = "Deployment invite code")]
        public string InviteCode { get; set; } = string.Empty;
    }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? RedirectToPage("/Dashboard/Index") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid || !SecureEquals(Input.InviteCode, options.Value.RegistrationInviteCode))
        {
            ModelState.AddModelError(string.Empty, "Registration could not be completed with the supplied details.");
            return Page();
        }

        var email = Input.Email.Trim().ToLowerInvariant();
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = Input.DisplayName.Trim() };
        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Registration could not be completed with the supplied details.");
            return Page();
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToPage("/Dashboard/Index");
    }

    private static bool SecureEquals(string left, string right)
    {
        var leftHash = SHA256.HashData(Encoding.UTF8.GetBytes(left ?? string.Empty));
        var rightHash = SHA256.HashData(Encoding.UTF8.GetBytes(right));
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }
}
