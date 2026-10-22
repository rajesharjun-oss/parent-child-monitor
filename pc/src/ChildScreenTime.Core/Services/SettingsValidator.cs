using System.Text.RegularExpressions;
using ChildScreenTime.Core.Models;

namespace ChildScreenTime.Core.Services;

public static partial class SettingsValidator
{
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.Ordinal)
    {
        "administrator",
        "childscreentime",
        "letmein123",
        "parentpassword",
        "password123",
        "qwerty123456",
        "welcome123"
    };

    [GeneratedRegex("^[A-Za-z0-9._-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex ProcessNamePattern();

    public static IReadOnlyList<string> Validate(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<string>();

        if (settings.SchemaVersion != AppSettings.CurrentSchemaVersion)
        {
            errors.Add("Unsupported settings schema version.");
        }

        if (settings.DailyLimitMinutes is < 1 or > 1440)
        {
            errors.Add("Daily limit must be between 1 and 1,440 minutes.");
        }

        if (settings.RetentionDays is < 1 or > 365)
        {
            errors.Add("Retention must be between 1 and 365 days.");
        }

        if (settings.IsConfigured && string.IsNullOrWhiteSpace(settings.ParentPasswordHash))
        {
            errors.Add("A parent password hash is required after setup.");
        }

        if (settings.AppRules.Count > 500)
        {
            errors.Add("No more than 500 application rules are allowed.");
        }

        foreach (var rule in settings.AppRules)
        {
            if (!ProcessNamePattern().IsMatch(RestrictionEvaluator.NormalizeProcessName(rule.ProcessName)))
            {
                errors.Add($"Invalid process name: {rule.ProcessName}");
            }

            if (rule.DailyLimitMinutes is < 1 or > 1440)
            {
                errors.Add($"The limit for {rule.ProcessName} must be between 1 and 1,440 minutes.");
            }
        }

        errors.AddRange(RemoteEndpointValidator.Validate(settings.RemoteSync));

        return errors;
    }

    public static IReadOnlyList<string> ValidateNewPassword(string password, string confirmation)
    {
        var errors = new List<string>();
        if (password.Length is < 12 or > 128)
        {
            errors.Add("Parent password must contain 12 to 128 characters.");
        }

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            errors.Add("Password confirmation does not match.");
        }

        if (password.Length >= 12)
        {
            var normalizedPassword = new string(password
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
            if (CommonPasswords.Contains(normalizedPassword))
            {
                errors.Add("Choose a password that is not commonly used or based on the product name.");
            }

            var characterClasses = new[]
            {
                password.Any(char.IsLower),
                password.Any(char.IsUpper),
                password.Any(char.IsDigit),
                password.Any(ch => !char.IsLetterOrDigit(ch))
            }.Count(value => value);

            if (characterClasses < 3)
            {
                errors.Add("Use at least three of: lowercase, uppercase, numbers, and symbols.");
            }
        }

        return errors;
    }
}
