using System.Security.Cryptography;
using System.Text;

namespace ContextDepot.Infrastructure.VectorStore;

public static class VectorCollectionNamePolicy
{
    public static string CreateProfileKey(string provider, string model, int dimensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimensions);

        var profile = string.Join('\n', provider.Trim(), model.Trim(), dimensions.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(profile));
        return Convert.ToHexString(digest)[..16].ToLowerInvariant();
    }

    public static string CreateContextCollectionName(string provider, string model, int dimensions) =>
        $"context_vectors_{CreateProfileKey(provider, model, dimensions)}";

    public static string CreateDocumentCollectionName(string provider, string model, int dimensions) =>
        $"document_vectors_{CreateProfileKey(provider, model, dimensions)}";
}
