# Remote parent dashboard deployment

The remote dashboard is an ASP.NET Core 10 application backed by PostgreSQL. It is designed to run behind an HTTPS reverse proxy. The Windows agent refuses cleartext remote servers except `localhost` development URLs.

## Data synchronized

The child agent uploads only:

- the paired device identifier and parent-assigned device name;
- local calendar date;
- executable/process name and friendly application name;
- cumulative active seconds;
- optional YouTube video ID, title, channel name, cumulative active playback seconds, and last-seen UTC timestamp when the visible browser companion is installed; and
- last-seen UTC timestamp.

It does not upload screenshots, arbitrary window titles, general browser URLs/history, keystrokes, messages, files, microphone or camera data, local parent passwords, or Windows credentials.

## Render preview deployment

The repository root includes a `render.yaml` Blueprint for a zero-cost preview deployment:

- one Free Docker web service;
- one Free Render Postgres database;
- HTTPS on the generated `onrender.com` hostname;
- private-only database networking;
- automatic EF Core migrations; and
- a health check at `/health`.

Create a new Render Blueprint from this repository and provide a high-entropy value for `CST_Remote__RegistrationInviteCode` when prompted. The generated Render hostname is automatically applied to ASP.NET Core's allowed-host configuration.

This preview profile is intended for evaluation, not long-term production. Free Render Postgres expires 30 days after creation and has no backups. Free web services use ephemeral local files, so a service restart can rotate the ASP.NET Core data-protection keys and sign existing browser sessions out. Parent accounts, paired devices, application usage, and YouTube summaries remain in PostgreSQL. Upgrade the database before the 30-day expiration and use a paid web service with persistent key storage before relying on the deployment for ongoing monitoring.

## Production prerequisites

- A Linux host or container platform with Docker Compose
- A DNS name such as `monitor.example.com`
- An HTTPS reverse proxy or managed load balancer with a valid certificate
- Encrypted backups for the PostgreSQL and data-protection-key volumes
- Firewall rules exposing only TCP 443 publicly; port 8080 remains bound to loopback

## Configure and start

1. Copy the example without committing the result:

   ```bash
   cp pc/src/ChildScreenTime.Remote/.env.example pc/src/ChildScreenTime.Remote/.env
   ```

2. Generate two different high-entropy secrets. Use at least 32 random bytes for the PostgreSQL password and the private registration invite code. Set `CST_AllowedHosts` to the exact public hostname.
3. Start the services:

   ```bash
   docker compose --env-file pc/src/ChildScreenTime.Remote/.env -f pc/remote-compose.yml up -d --build
   ```

4. Configure the reverse proxy to send HTTPS traffic to `http://127.0.0.1:8080`. Preserve the original host and reject unrecognized hostnames. Do not expose PostgreSQL or port 8080 publicly.
5. Open `https://monitor.example.com/health` and confirm a healthy response, then create the first parent account using the private invite code.
6. Open **Security** and enable authenticator-app two-factor authentication. Store the recovery codes in a password manager.

The initial EF Core migration runs when `CST_Remote__ApplyMigrationsOnStartup=true`. For multi-instance production deployments, run migrations as a single release job and set this value to `false` on normal application instances.

## Pair the child PC

1. Sign in to the parent dashboard and select **Pair a PC**.
2. Re-enter the parent account password to create a single-use pairing code. The code expires after ten minutes.
3. On the child PC, open **Parent controls → Remote viewing**.
4. Enter the dashboard HTTPS URL, a recognizable device name, and the pairing code.
5. Select **Pair this PC**. The first usage upload runs immediately; later uploads run about every five minutes and retry after connectivity returns.
6. If YouTube watch activity is required, install the optional visible Chrome/Edge companion on the child PC using `pc/browser-extension/README.md`.

The dashboard can revoke a device at any time. Revocation is checked by the server on every upload. **Delete history and revoke** permanently removes that device's synchronized records and prevents further uploads. Disconnecting locally deletes the DPAPI-protected device credential from the PC but intentionally does not delete records already stored on the server. Clear the local history before pairing again if previously deleted local records must not be uploaded to a new device record.

## Operations and recovery

- Back up both `remote-database` and `remote-keys`. Losing the key volume signs out all browsers and invalidates protected authentication artifacts.
- Restore backups into an isolated staging environment before relying on them.
- Rotate the registration invite code after account enrollment. Changing it does not affect existing accounts.
- Review container and reverse-proxy logs, but never enable request-body or authorization-header logging.
- Apply .NET, PostgreSQL, container-base-image, and NuGet security updates promptly.
- Use `docker compose ... down` for maintenance. Do not add `-v` unless permanently deleting the database and key volumes is intentional.

## Local development

Provide the settings through user-secrets or environment variables; never place real credentials in `appsettings.json`:

```powershell
$env:CST_ConnectionStrings__RemoteDatabase = 'Host=localhost;Database=child_screen_time;Username=child_screen_time;Password=development-only'
$env:CST_Remote__RegistrationInviteCode = 'development-invite-code-at-least-20-characters'
$env:CST_Remote__DataProtectionKeyPath = '.local-keys'
dotnet run --project pc/src/ChildScreenTime.Remote/ChildScreenTime.Remote.csproj
```
