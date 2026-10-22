using ChildScreenTime.Core.Models;

namespace ChildScreenTime.Core.Services;

public static class RemoteEndpointValidator
{
    public static IReadOnlyList<string> Validate(RemoteSyncSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<string>();
        var hasAnyCredential =
            !string.IsNullOrWhiteSpace(settings.DeviceId) ||
            !string.IsNullOrWhiteSpace(settings.DeviceToken);

        if (string.IsNullOrWhiteSpace(settings.ServerBaseUrl))
        {
            if (hasAnyCredential)
            {
                errors.Add("A remote server URL is required for stored device credentials.");
            }
            return errors;
        }

        if (settings.ServerBaseUrl.Length > 2048 ||
            !Uri.TryCreate(settings.ServerBaseUrl, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) && !IsLoopbackHttp(uri)))
        {
            errors.Add("Remote viewing requires an HTTPS server URL. HTTP is allowed only for local development.");
        }

        if (settings.DeviceName.Length is < 1 or > 100)
        {
            errors.Add("The remote device name must contain 1 to 100 characters.");
        }

        if (hasAnyCredential && (!Guid.TryParse(settings.DeviceId, out _) || settings.DeviceToken.Length is < 40 or > 200))
        {
            errors.Add("The stored remote device credential is invalid.");
        }

        return errors;
    }

    public static Uri NormalizeServerUrl(string value)
    {
        var candidate = new RemoteSyncSettings { ServerBaseUrl = value.Trim(), DeviceName = "Validation" };
        var errors = Validate(candidate);
        if (errors.Count > 0 || !Uri.TryCreate(candidate.ServerBaseUrl, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(value));
        }

        var builder = new UriBuilder(uri) { Path = uri.AbsolutePath.TrimEnd('/') + "/" };
        return builder.Uri;
    }

    private static bool IsLoopbackHttp(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback;
}
