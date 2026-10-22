using ChildScreenTime.Core.Models;
using ChildScreenTime.Core.Services;

namespace ChildScreenTime.Core.Tests;

[TestClass]
public sealed class SettingsValidatorTests
{
    [TestMethod]
    public void RejectsPathLikeProcessNames()
    {
        var settings = ValidSettings();
        settings.AppRules.Add(new AppRule { ProcessName = "..\\bad.exe", DailyLimitMinutes = 10 });

        var errors = SettingsValidator.Validate(settings);

        Assert.IsTrue(errors.Any(message => message.Contains("Invalid process name", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void RejectsUnboundedLimits()
    {
        var settings = ValidSettings();
        settings.DailyLimitMinutes = 0;

        var errors = SettingsValidator.Validate(settings);

        Assert.IsNotEmpty(errors);
    }

    [TestMethod]
    public void PasswordRequiresLengthConfirmationAndComplexity()
    {
        Assert.IsNotEmpty(SettingsValidator.ValidateNewPassword("short", "different"));
        Assert.IsNotEmpty(SettingsValidator.ValidateNewPassword("Password123!", "Password123!"));
        Assert.IsEmpty(SettingsValidator.ValidateNewPassword("A strong parent password 2026!", "A strong parent password 2026!"));
    }

    private static AppSettings ValidSettings() => new()
    {
        IsConfigured = true,
        ParentPasswordHash = "hash"
    };
}
