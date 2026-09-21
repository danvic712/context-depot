using System.Security.Cryptography;

namespace ContextDepot.Infrastructure.CurrentDepot;

public sealed class DepotAccessKeySecretHasher
{
    private const string Marker = "cdk_";
    private const int PublicPartByteCount = 12;
    private const int SecretPartByteCount = 32;

    public GeneratedDepotAccessKey Generate()
    {
        var publicPart = Base64UrlEncode(RandomNumberGenerator.GetBytes(PublicPartByteCount));
        var secretPart = Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretPartByteCount));
        var prefix = Marker + publicPart;
        var plaintext = prefix + "." + secretPart;
        return new GeneratedDepotAccessKey(plaintext, prefix, Hash(plaintext));
    }

    public bool Verify(string presentedKey, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(presentedKey) ||
            string.IsNullOrWhiteSpace(expectedHash) ||
            expectedHash.Length != 64)
        {
            return false;
        }

        byte[] expectedBytes;
        try
        {
            expectedBytes = Convert.FromHexString(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        if (expectedBytes.Length != 32)
        {
            return false;
        }

        Span<byte> actualBytes = stackalloc byte[32];
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(presentedKey), actualBytes);
        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    public bool TryGetPrefix(string presentedKey, out string prefix)
    {
        prefix = string.Empty;
        if (string.IsNullOrWhiteSpace(presentedKey) || !presentedKey.StartsWith(Marker, StringComparison.Ordinal))
        {
            return false;
        }

        var separatorIndex = presentedKey.IndexOf('.', Marker.Length);
        if (separatorIndex <= Marker.Length || separatorIndex > 31 || separatorIndex == presentedKey.Length - 1)
        {
            return false;
        }

        prefix = presentedKey[..separatorIndex];
        return true;
    }

    private static string Hash(string plaintext) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plaintext)));

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed record GeneratedDepotAccessKey(string Plaintext, string Prefix, string SecretHash);
