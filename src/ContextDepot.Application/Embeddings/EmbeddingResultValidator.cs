using ContextDepot.Application.Shared.Exceptions;
using Microsoft.Extensions.AI;

namespace ContextDepot.Application.Embeddings;

public sealed class EmbeddingResultValidator
{
    public IReadOnlyList<ReadOnlyMemory<float>> Validate(
        GeneratedEmbeddings<Embedding<float>>? generated,
        int expectedCount,
        int expectedDimensions)
    {
        if (generated is null || generated.Count != expectedCount)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.EmbeddingGeneratorInvalidResponse);
        }

        var vectors = new ReadOnlyMemory<float>[generated.Count];
        for (var index = 0; index < generated.Count; index++)
        {
            var embedding = generated[index];
            if (embedding is null)
            {
                throw new ContextDepotApplicationException(ApplicationErrorCodes.EmbeddingGeneratorInvalidResponse);
            }

            var vector = embedding.Vector;
            if (vector.Length != expectedDimensions)
            {
                throw new ContextDepotApplicationException(ApplicationErrorCodes.EmbeddingDimensionMismatch);
            }

            var hasNonFiniteValue = false;
            foreach (var value in vector.Span)
            {
                if (!float.IsFinite(value))
                {
                    hasNonFiniteValue = true;
                    break;
                }
            }

            if (hasNonFiniteValue)
            {
                throw new ContextDepotApplicationException(ApplicationErrorCodes.EmbeddingGeneratorInvalidResponse);
            }

            vectors[index] = vector;
        }

        return vectors;
    }
}
