using System.Buffers;
using System.Buffers.Text;
using ContextDepot.Application.Settings.Contracts;
using System.Security.Cryptography;

namespace ContextDepot.Infrastructure.CurrentDepot;

public sealed class DepotAccessKeySecretHasher : IAccessKeySecretGenerator
{
    private const string Marker = "cdk_";
    private const int PublicPartByteCount = 12;
    private const int SecretPartByteCount = 32;

    public GeneratedAccessKey Generate()
    {
        var publicPart = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(PublicPartByteCount));
        var secretPart = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretPartByteCount));
        var prefix = Marker + publicPart;
        var plaintext = prefix + "." + secretPart;
        return new GeneratedAccessKey(plaintext, prefix, Hash(plaintext));
    }

    public bool Verify(string presentedKey, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(presentedKey) ||
            string.IsNullOrWhiteSpace(expectedHash) ||
            expectedHash.Length != 64)
        {
            return false;
        }

        Span<byte> expectedBytes = stackalloc byte[SHA256.HashSizeInBytes];
        if (Convert.FromHexString(expectedHash, expectedBytes, out _, out var bytesWritten) != OperationStatus.Done ||
            bytesWritten != expectedBytes.Length)
        {
            return false;
        }

        Span<byte> actualBytes = stackalloc byte[SHA256.HashSizeInBytes];
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

}
