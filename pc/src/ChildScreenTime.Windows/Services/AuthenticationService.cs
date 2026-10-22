using ChildScreenTime.Core.Services;

namespace ChildScreenTime.Windows.Services;

public sealed record AuthenticationResult(bool Succeeded, string Message, TimeSpan? RetryAfter = null);

public sealed class AuthenticationService
{
    private const int WorkFactor = 12;
    private const int MaximumFailures = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private readonly SettingsRepository settingsRepository;
    private readonly AuditRepository auditRepository;

    public AuthenticationService(SettingsRepository settingsRepository, AuditRepository auditRepository)
    {
        this.settingsRepository = settingsRepository;
        this.auditRepository = auditRepository;
    }

    public static string CreatePasswordHash(string password, string confirmation)
    {
        var errors = SettingsValidator.ValidateNewPassword(password, confirmation);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors));
        }

        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public AuthenticationResult Verify(string password)
    {
        var settings = settingsRepository.GetSnapshot();
        var now = DateTimeOffset.UtcNow;
        if (settings.AuthenticationLockedUntilUtc is { } lockedUntil && lockedUntil > now)
        {
            var remaining = lockedUntil - now;
            return new(false, $"Too many attempts. Try again in {Math.Ceiling(remaining.TotalMinutes):0} minute(s).", remaining);
        }

        var valid = false;
        try
        {
            valid = !string.IsNullOrEmpty(settings.ParentPasswordHash) &&
                    BCrypt.Net.BCrypt.Verify(password, settings.ParentPasswordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            auditRepository.Add("security_error", "Stored parent credential could not be verified.");
        }

        if (valid)
        {
            settingsRepository.Update(candidate =>
            {
                candidate.FailedAuthenticationCount = 0;
                candidate.AuthenticationLockedUntilUtc = null;
                if (BCrypt.Net.BCrypt.PasswordNeedsRehash(candidate.ParentPasswordHash, WorkFactor))
                {
                    candidate.ParentPasswordHash = BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
                }
            });
            auditRepository.Add("parent_authentication", "Parent controls unlocked successfully.");
            return new(true, "Parent controls unlocked.");
        }

        var updated = settingsRepository.Update(candidate =>
        {
            candidate.FailedAuthenticationCount++;
            if (candidate.FailedAuthenticationCount >= MaximumFailures)
            {
                candidate.AuthenticationLockedUntilUtc = now.Add(LockoutDuration);
                candidate.FailedAuthenticationCount = 0;
            }
        });
        auditRepository.Add("parent_authentication_failed", "An incorrect parent password was entered.");

        return updated.AuthenticationLockedUntilUtc is { } until && until > now
            ? new(false, "Too many attempts. Parent controls are locked for 5 minutes.", LockoutDuration)
            : new(false, "The parent password is incorrect.");
    }
}
