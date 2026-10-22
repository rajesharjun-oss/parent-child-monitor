using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildScreenTime.Core.Models;
using ChildScreenTime.Core.Services;

namespace ChildScreenTime.Windows.Services;

public sealed record RemoteSyncStatus(bool IsPaired, bool IsSyncing, DateTimeOffset? LastSuccessUtc, string Message);

public sealed class RemoteSyncService : IAsyncDisposable
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(20);
    private const int BatchSize = 500;

    private readonly SettingsRepository settings;
    private readonly UsageRepository usage;
    private readonly AuditRepository audit;
    private readonly Func<HttpMessageHandler> handlerFactory;
    private readonly CancellationTokenSource cancellation = new();
    private readonly SemaphoreSlim syncGate = new(1, 1);
    private Task? backgroundTask;

    public RemoteSyncService(
        SettingsRepository settings,
        UsageRepository usage,
        AuditRepository audit,
        Func<HttpMessageHandler>? handlerFactory = null)
    {
        this.settings = settings;
        this.usage = usage;
        this.audit = audit;
        this.handlerFactory = handlerFactory ?? CreateHandler;
        var remote = settings.GetSnapshot().RemoteSync;
        Status = new RemoteSyncStatus(remote.IsPaired, false, remote.LastSuccessfulSyncUtc, remote.IsPaired ? "Waiting to synchronize." : "Not paired.");
    }

    public RemoteSyncStatus Status { get; private set; }

    public event EventHandler<RemoteSyncStatus>? StatusChanged;

    public void Start() => backgroundTask ??= RunAsync(cancellation.Token);

    public async Task PairAsync(string serverUrl, string pairingCode, string deviceName, CancellationToken cancellationToken = default)
    {
        var baseUri = RemoteEndpointValidator.NormalizeServerUrl(serverUrl);
        var normalizedCode = pairingCode.Trim();
        var normalizedName = deviceName.Trim();
        if (normalizedCode.Length is < 20 or > 200 || normalizedName.Length is < 1 or > 100)
        {
            throw new ArgumentException("Enter a valid pairing code and a device name containing 1 to 100 characters.");
        }

        using var client = CreateClient(baseUri);
        using var response = await client.PostAsJsonAsync(
            "api/v1/devices/pair",
            new PairDeviceRequest(normalizedCode, normalizedName),
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("The pairing code is invalid or has expired.");
        }
        if ((int)response.StatusCode == 429)
        {
            throw new InvalidOperationException("Too many pairing attempts. Wait a few minutes and try again.");
        }
        response.EnsureSuccessStatusCode();

        var paired = await response.Content.ReadFromJsonAsync<PairDeviceResponse>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The remote server returned an invalid pairing response.");
        if (paired.DeviceId == Guid.Empty || paired.DeviceToken is null || paired.DeviceToken.Length is < 40 or > 200)
        {
            throw new InvalidDataException("The remote server returned an invalid device credential.");
        }

        settings.Update(value =>
        {
            value.RemoteSync.ServerBaseUrl = baseUri.GetLeftPart(UriPartial.Authority) + baseUri.AbsolutePath.TrimEnd('/');
            value.RemoteSync.DeviceId = paired.DeviceId.ToString("D");
            value.RemoteSync.DeviceToken = paired.DeviceToken;
            value.RemoteSync.DeviceName = normalizedName;
            value.RemoteSync.LastSuccessfulSyncUtc = null;
        });
        audit.Add("remote_device_paired", "This PC was paired with a remote parent dashboard.");
        UpdateStatus(new RemoteSyncStatus(true, false, null, "Paired. The first upload will start now."));
        await SyncNowAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SyncNowAsync(CancellationToken cancellationToken = default)
    {
        if (!await syncGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var remote = settings.GetSnapshot().RemoteSync;
            if (!remote.IsPaired)
            {
                UpdateStatus(new RemoteSyncStatus(false, false, null, "Not paired."));
                return;
            }

            UpdateStatus(new RemoteSyncStatus(true, true, remote.LastSuccessfulSyncUtc, "Synchronizing usage summaries..."));
            var syncStartedUtc = DateTimeOffset.UtcNow;
            var overlapBoundary = remote.LastSuccessfulSyncUtc?.AddMinutes(-10);
            var records = usage.GetAll()
                .Where(item => overlapBoundary is null || item.LastSeenUtc >= overlapBoundary.Value)
                .OrderBy(item => item.LocalDate)
                .ThenBy(item => item.ProcessName, StringComparer.OrdinalIgnoreCase)
                .Select(item => new UsageUploadItem(
                    item.LocalDate,
                    item.ProcessName,
                    item.DisplayName,
                    item.ActiveSeconds,
                    item.LastSeenUtc))
                .ToArray();

            var baseUri = RemoteEndpointValidator.NormalizeServerUrl(remote.ServerBaseUrl);
            using var client = CreateClient(baseUri);
            for (var offset = 0; offset < records.Length; offset += BatchSize)
            {
                var batch = records.Skip(offset).Take(BatchSize).ToArray();
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/usage/batch")
                {
                    Content = JsonContent.Create(new UsageUploadRequest(batch))
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Device", $"{remote.DeviceId}.{remote.DeviceToken}");
                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    throw new InvalidOperationException("Remote access was revoked. Pair this PC again from the parent dashboard.");
                }
                if ((int)response.StatusCode == 429)
                {
                    throw new InvalidOperationException("The remote server is busy. Synchronization will retry automatically.");
                }
                response.EnsureSuccessStatusCode();
            }

            settings.Update(value => value.RemoteSync.LastSuccessfulSyncUtc = syncStartedUtc);
            UpdateStatus(new RemoteSyncStatus(true, false, syncStartedUtc, $"Synchronized {records.Length} usage summaries."));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or InvalidDataException)
        {
            var remote = settings.GetSnapshot().RemoteSync;
            UpdateStatus(new RemoteSyncStatus(remote.IsPaired, false, remote.LastSuccessfulSyncUtc, exception.Message));
        }
        finally
        {
            syncGate.Release();
        }
    }

    public void Unpair()
    {
        settings.Update(value => value.RemoteSync = new RemoteSyncSettings { DeviceName = Environment.MachineName });
        audit.Add("remote_device_unpaired", "Remote synchronization credentials were removed from this PC.");
        UpdateStatus(new RemoteSyncStatus(false, false, null, "Not paired."));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(InitialDelay, cancellationToken).ConfigureAwait(false);
            while (!cancellationToken.IsCancellationRequested)
            {
                await SyncNowAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(SyncInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private HttpClient CreateClient(Uri baseUri) => new(handlerFactory(), disposeHandler: true)
    {
        BaseAddress = baseUri,
        Timeout = TimeSpan.FromSeconds(20),
        DefaultRequestHeaders = { UserAgent = { new ProductInfoHeaderValue("ChildScreenTime", "1.2") } }
    };

    private static HttpClientHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    };

    private void UpdateStatus(RemoteSyncStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, status);
    }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        if (backgroundTask is not null)
        {
            await backgroundTask.ConfigureAwait(false);
        }
        cancellation.Dispose();
        syncGate.Dispose();
    }
}
