using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ContextDepot.Infrastructure.Embeddings;

public static class EmbeddingProfileFingerprint
{
    public static string Compute(
        string providerName,
        string protocolCode,
        string baseUrl,
        string modelName,
        int dimensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(protocolCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimensions);

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("The inference provider URL must use HTTP or HTTPS.", nameof(baseUrl));
        }

        var canonicalProfile = string.Join('\n',
            Normalize(providerName),
            Normalize(protocolCode).ToLowerInvariant(),
            endpoint.AbsoluteUri,
            Normalize(modelName),
            dimensions.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalProfile)));
    }

    private static string Normalize(string value) => value.Trim().Normalize(NormalizationForm.FormC);
}
