# Child Screen Time for Windows

Child Screen Time for Windows is a visible, local-first parental-control agent for Windows 10 and Windows 11. It records foreground application names and active duration, applies daily and per-application limits, enforces bedtime schedules, and provides password-protected parent controls.

It deliberately does **not** collect window titles, browser URLs, keystrokes, screenshots, document names, messages, microphone audio, camera video, or credentials.

## Features

- Foreground application usage aggregated by local day
- Five-minute idle detection so unattended time is not charged
- Global daily limit and overnight bedtime schedule
- Per-process daily limits and always-blocked rules
- Multi-monitor restriction overlay with parent-authorized temporary override
- BCrypt password hashing with persistent five-attempt lockout
- Windows DPAPI-protected settings, usage data, and audit history
- Explicit monitoring status, tray icon, retention controls, CSV export, and deletion
- Parent re-authentication for password changes, history deletion, and agent shutdown
- Automatic boot-time Windows service with active-session launch and recovery
- Automatic relaunch within approximately five seconds if the monitoring agent is closed

## Requirements

- 64-bit Windows 10 version 2004 or newer, or Windows 11
- .NET 10 SDK to build from source
- Inno Setup 6 to create the installer
- A standard, non-administrator Windows account for the child

An administrator can bypass any software running on a PC. The child account must therefore be a standard Windows account, and the parent must retain the administrator credentials.

## Build and test

From PowerShell:

```powershell
cd pc
.\build.ps1 -SkipInstaller
```

The script restores pinned packages, builds with warnings treated as errors, runs the rules-engine tests, and publishes a self-contained `win-x64` application to `pc/artifacts/publish`.

To build the installer, install Inno Setup 6 and run:

```powershell
.\build.ps1
```

The installer is written to `pc/artifacts/installer/ChildScreenTime-Setup.exe`.

## Installation

1. Sign the installer and application executable with an organization-owned Authenticode certificate before external distribution.
2. Sign in to the child's standard Windows account. This is a Windows sign-in only; the child never signs into Child Screen Time.
3. Run `ChildScreenTime-Setup.exe` normally, then approve the administrator prompt using the parent's administrator credentials.
4. Complete first-run setup and create a unique parent password of at least 12 characters.
5. Confirm the `ChildScreenTimeAgent` Windows service is running and the tray icon appears after Windows sign-in.
6. Close the agent after entering the parent password and confirm the service relaunches it within approximately five seconds.

Before uninstalling, use **Delete all activity history** in Parent controls if the usage history should be removed. Uninstalling removes the application but deliberately preserves protected configuration and audit data for safe repair or reinstall. A parent can remove the remaining `%LOCALAPPDATA%\ChildScreenTime` directory after uninstalling if the device is being decommissioned.

## Data and security model

- All application data is stored under `%LOCALAPPDATA%\ChildScreenTime` and protected with Windows DPAPI for the enrolled Windows user.
- Parent passwords are BCrypt hashes with work factor 12. Plaintext passwords are never stored or logged.
- Authentication failures are recorded without recording attempted passwords.
- CSV exports are intentionally plaintext because the parent chooses their destination; they should be stored securely.
- There is no network listener, cloud synchronization, hidden mode, remote command channel, or telemetry.
- The `ChildScreenTimeAgent` service runs as LocalSystem from boot. It does not inspect activity in session 0; it launches the visible agent in the currently active Windows desktop session, where foreground-window monitoring is technically possible.

## Current limitations

- Enforcement is designed for a standard child account. A Windows administrator can disable or remove it.
- A standard user can terminate the visible agent, but the protected Windows service relaunches the installed agent. A Windows administrator can still stop or uninstall the service.
- Settings and activity data remain scoped to the Windows account configured during installation. Shared multi-account policy storage requires a future service-owned data channel; test this release with one dedicated standard child account.
- Application rules use process names. Digitally signed publisher policies and browser category filtering are not included in this local-first release.
- The generated installer is unsigned until an Authenticode certificate is supplied.
- Sleep, shutdown, Windows security screens, and operating-system updates remain available and are not intercepted.
