using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Application.SemanticRetrieval;

public sealed class QueryEmbeddingCache(EmbeddingGeneratorService embeddingGenerator)
{
    private readonly object gate = new();
    private string? cachedQuery;
    private Task<ReadOnlyMemory<float>>? cachedEmbedding;

    public Task<ReadOnlyMemory<float>> GetOrCreateAsync(
        string normalizedQuery,
        CancellationToken cancellationToken)
    {
        var query = Normalize(normalizedQuery);
        lock (gate)
        {
            if (cachedQuery is not null && !string.Equals(cachedQuery, query, StringComparison.Ordinal))
            {
                throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidSearchQuery);
            }

            cachedQuery ??= query;
            return cachedEmbedding ??= GenerateAsync(query, cancellationToken);
        }
    }

    private async Task<ReadOnlyMemory<float>> GenerateAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var embeddings = await embeddingGenerator.GenerateAsync([query], cancellationToken);
        if (embeddings.Count != 1)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.EmbeddingGeneratorInvalidResponse);
        }

        return embeddings[0];
    }

    private static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length == 0)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidSearchQuery);
        }

        return normalized;
    }
}
