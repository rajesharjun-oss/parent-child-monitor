using System.ComponentModel.DataAnnotations;

namespace ChildScreenTime.Remote.Configuration;

public sealed class RemoteOptions
{
    public const string SectionName = "Remote";

    [Required, MinLength(20), MaxLength(200)]
    public string RegistrationInviteCode { get; set; } = string.Empty;

    [Range(5, 30)]
    public int PairingCodeLifetimeMinutes { get; set; } = 10;

    [Required]
    public string DataProtectionKeyPath { get; set; } = "keys";

    public bool ApplyMigrationsOnStartup { get; set; }

    [Range(30, 366)]
    public int DataRetentionDays { get; set; } = 90;
}
