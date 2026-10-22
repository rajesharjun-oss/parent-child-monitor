using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ChildScreenTime.Core.Services;

namespace ChildScreenTime.Service;

internal sealed class SessionAgentSupervisor : IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);
    private readonly CancellationTokenSource cancellation = new();
    private readonly SemaphoreSlim immediateCheck = new(0, 1);
    private readonly ServiceLog log = new();
    private Task? worker;
    private bool disposed;

    public void Start()
    {
        worker ??= Task.Run(() => RunAsync(cancellation.Token));
        log.Information("Boot-time agent supervision started.");
        RequestImmediateCheck();
    }

    public void RequestImmediateCheck()
    {
        if (!disposed && immediateCheck.CurrentCount == 0)
        {
            immediateCheck.Release();
        }
    }

    public async Task StopAsync()
    {
        if (cancellation.IsCancellationRequested)
        {
            return;
        }

        cancellation.Cancel();
        RequestImmediateCheck();
        if (worker is not null)
        {
            try
            {
                await worker.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during service shutdown.
            }
        }

        log.Information("Agent supervision stopped.");
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            CheckActiveSession();

            await immediateCheck.WaitAsync(CheckInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private void CheckActiveSession()
    {
        try
        {
            var agentPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "ChildScreenTime.exe"));
            if (!File.Exists(agentPath))
            {
                log.Error("The installed monitoring agent is missing.");
                return;
            }

            var activeSessionId = NativeMethods.WTSGetActiveConsoleSessionId();
            var runningSessions = GetRunningAgentSessions(agentPath);
            if (AgentLaunchPolicy.ShouldLaunch(activeSessionId, runningSessions))
            {
                LaunchAgent(agentPath, activeSessionId);
            }
        }
        catch (Exception exception)
        {
            log.Error($"Agent supervision check failed: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static HashSet<uint> GetRunningAgentSessions(string expectedPath)
    {
        var sessions = new HashSet<uint>();
        foreach (var process in Process.GetProcessesByName("ChildScreenTime"))
        {
            using (process)
            {
                try
                {
                    var processPath = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(processPath) &&
                        string.Equals(Path.GetFullPath(processPath), expectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        sessions.Add((uint)process.SessionId);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process exited while it was being inspected.
                }
                catch (Win32Exception)
                {
                    // Ignore unrelated or inaccessible processes with the same name.
                }
            }
        }

        return sessions;
    }

    private void LaunchAgent(string agentPath, uint sessionId)
    {
        if (!NativeMethods.WTSQueryUserToken(sessionId, out var userToken))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to obtain the active Windows session token.");
        }

        try
        {
            var attributes = new NativeMethods.SecurityAttributes
            {
                Length = Marshal.SizeOf<NativeMethods.SecurityAttributes>()
            };
            if (!NativeMethods.DuplicateTokenEx(
                    userToken,
                    NativeMethods.TokenAllAccess,
                    ref attributes,
                    NativeMethods.SecurityImpersonationLevel.Identification,
                    NativeMethods.TokenType.Primary,
                    out var primaryToken))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create an active-session token.");
            }

            try
            {
                var hasEnvironment = NativeMethods.CreateEnvironmentBlock(out var environment, primaryToken, false);
                if (!hasEnvironment)
                {
                    environment = IntPtr.Zero;
                }

                try
                {
                    var startupInfo = new NativeMethods.StartupInfo
                    {
                        Size = Marshal.SizeOf<NativeMethods.StartupInfo>(),
                        Desktop = @"winsta0\default"
                    };
                    var commandLine = $"\"{agentPath}\" --background --service-managed";
                    var workingDirectory = Path.GetDirectoryName(agentPath)
                                           ?? throw new InvalidOperationException("The installed agent path has no parent directory.");

                    if (!NativeMethods.CreateProcessAsUser(
                            primaryToken,
                            null,
                            commandLine,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            false,
                            NativeMethods.CreateUnicodeEnvironment,
                            environment,
                            workingDirectory,
                            ref startupInfo,
                            out var processInformation))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to launch monitoring in the active Windows session.");
                    }

                    NativeMethods.CloseHandle(processInformation.Thread);
                    NativeMethods.CloseHandle(processInformation.Process);
                    log.Information($"Monitoring agent launched in Windows session {sessionId}.");
                }
                finally
                {
                    if (environment != IntPtr.Zero)
                    {
                        NativeMethods.DestroyEnvironmentBlock(environment);
                    }
                }
            }
            finally
            {
                NativeMethods.CloseHandle(primaryToken);
            }
        }
        finally
        {
            NativeMethods.CloseHandle(userToken);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (!cancellation.IsCancellationRequested)
        {
            StopAsync().GetAwaiter().GetResult();
        }

        cancellation.Dispose();
        immediateCheck.Dispose();
        disposed = true;
    }
}
