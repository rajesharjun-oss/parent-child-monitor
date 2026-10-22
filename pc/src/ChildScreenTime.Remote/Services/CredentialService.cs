using System.Security.Cryptography;

namespace ChildScreenTime.Remote.Services;

public sealed class CredentialService
{
    public string CreatePairingCode() => Encode(RandomNumberGenerator.GetBytes(24));

    public string CreateDeviceToken() => Encode(RandomNumberGenerator.GetBytes(32));

    public string Hash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    }

    public bool Verify(string value, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(value) || expectedHash.Length != 64)
        {
            return false;
        }

        var actual = Convert.FromHexString(Hash(value));
        var expected = Convert.FromHexString(expectedHash);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
