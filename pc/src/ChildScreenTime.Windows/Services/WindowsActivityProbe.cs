using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ChildScreenTime.Windows.Services;

public sealed record ForegroundApplication(string ProcessName, string DisplayName);

public sealed class WindowsActivityProbe
{
    private readonly int currentProcessId = Environment.ProcessId;

    public ForegroundApplication? GetForegroundApplication()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        _ = GetWindowThreadProcessId(handle, out var processId);
        if (processId == 0 || processId == currentProcessId)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            var processName = process.ProcessName;
            var displayName = processName;
            try
            {
                displayName = process.MainModule?.FileVersionInfo.FileDescription?.Trim() ?? processName;
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException)
            {
                // Process metadata can be protected; the process name is sufficient and contains no content.
            }

            return new ForegroundApplication(processName, string.IsNullOrWhiteSpace(displayName) ? processName : displayName);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }

    public static bool IsUserActive(TimeSpan maximumIdleTime)
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
        {
            return true;
        }

        var idleMilliseconds = unchecked((uint)Environment.TickCount - info.Time);
        return idleMilliseconds <= maximumIdleTime.TotalMilliseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo lastInputInfo);
}
