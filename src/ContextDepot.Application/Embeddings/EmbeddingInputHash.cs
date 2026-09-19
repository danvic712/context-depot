using System.Security.Cryptography;
using System.Text;

namespace ContextDepot.Application.Embeddings;

public static class EmbeddingInputHash
{
    public static string Compute(string embeddingInput)
    {
        ArgumentNullException.ThrowIfNull(embeddingInput);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(embeddingInput))).ToLowerInvariant();
    }
}
