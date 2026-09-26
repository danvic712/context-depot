using ContextDepot.Application.SemanticRetrieval.Contracts;
using ContextDepot.Application.SemanticRetrieval.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Application.SemanticRetrieval;

internal sealed class SemanticCandidateSearcher(ISemanticRetrievalRepository repository)
{
    public async Task<(IReadOnlyList<SemanticContextCandidateRecord> Contexts,
        IReadOnlyList<SemanticDocumentCandidateRecord> Documents,
        bool Used,
        bool Degraded)> SearchAsync(
        SemanticCandidateQuery query,
        string queryText,
        QueryEmbeddingCache embeddingCache,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var queryVector = await embeddingCache.GetOrCreateAsync(queryText, cancellationToken);
            var contexts = await repository.FindContextCandidatesAsync(query, queryVector, cancellationToken);
            var documents = await repository.FindDocumentCandidatesAsync(query, queryVector, cancellationToken);
            return (contexts, documents, true, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ContextDepotApplicationException exception) when (IsSemanticDegradation(exception.ErrorCode))
        {
            logger.LogWarning(
                exception,
                "{ErrorCode} degraded semantic retrieval for the current read request.",
                exception.ErrorCode);
            return ([], [], false, true);
        }
    }

    private static bool IsSemanticDegradation(string errorCode) =>
        errorCode is ApplicationErrorCodes.EmbeddingGeneratorUnavailable
            or ApplicationErrorCodes.EmbeddingGeneratorInvalidResponse
            or ApplicationErrorCodes.EmbeddingDimensionMismatch
            or ApplicationErrorCodes.SecretContentRejected
            or ApplicationErrorCodes.VectorSearchFailed;
}
