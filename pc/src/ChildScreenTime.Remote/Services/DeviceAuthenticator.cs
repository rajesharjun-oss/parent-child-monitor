using ChildScreenTime.Remote.Data;
using ChildScreenTime.Remote.Models;
using Microsoft.EntityFrameworkCore;

namespace ChildScreenTime.Remote.Services;

public sealed class DeviceAuthenticator(ApplicationDbContext database, CredentialService credentials)
{
    public async Task<ChildDevice?> AuthenticateAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var authorization = request.Headers.Authorization.ToString();
        const string prefix = "Device ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var credential = authorization[prefix.Length..];
        var separator = credential.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0 || separator == credential.Length - 1 ||
            !Guid.TryParse(credential[..separator], out var deviceId))
        {
            return null;
        }

        var token = credential[(separator + 1)..];
        if (token.Length is < 40 or > 200)
        {
            return null;
        }

        var device = await database.ChildDevices.SingleOrDefaultAsync(
            value => value.Id == deviceId && value.RevokedUtc == null,
            cancellationToken).ConfigureAwait(false);
        return device is not null && credentials.Verify(token, device.TokenHash) ? device : null;
    }
}
