using Microsoft.Win32;

namespace ChildScreenTime.Windows.Services;

public static class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ChildScreenTime";
    private const string ServiceKeyPath = @"SYSTEM\CurrentControlSet\Services\ChildScreenTimeAgent";

    public static void EnsureAppropriateRegistration(bool serviceManaged)
    {
        if (serviceManaged || IsServiceInstalled())
        {
            RemoveLegacyRegistration();
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(ValueName, $"\"{executablePath}\" --background", RegistryValueKind.String);
    }

    private static bool IsServiceInstalled()
    {
        using var key = Registry.LocalMachine.OpenSubKey(ServiceKeyPath, writable: false);
        return key is not null;
    }

    private static void RemoveLegacyRegistration()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
