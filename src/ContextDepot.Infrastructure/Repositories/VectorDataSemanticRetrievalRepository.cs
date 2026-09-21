using System.Linq.Expressions;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.SemanticRetrieval.Contracts;
using ContextDepot.Application.SemanticRetrieval.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Documents.Enums;
using ContextDepot.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class VectorDataSemanticRetrievalRepository : ISemanticRetrievalRepository
{
    private readonly ContextDepotDbContext db;
    private readonly ILogger<VectorDataSemanticRetrievalRepository> logger;
    private readonly VectorStoreCollection<Guid, ContextVectorRecord> contextCollection;
    private readonly VectorStoreCollection<Guid, DocumentVectorRecord> documentCollection;
    private readonly int oversampleFactor;

    public VectorDataSemanticRetrievalRepository(
        PostgreSqlVectorStore vectorStore,
        ContextDepotDbContext db,
        IOptions<EmbeddingOptions> embeddingOptions,
        IOptions<RetrievalOptions> retrievalOptions,
        ILogger<VectorDataSemanticRetrievalRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(vectorStore);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(embeddingOptions);
        ArgumentNullException.ThrowIfNull(retrievalOptions);
        this.db = db;
        this.logger = logger;
        var profile = EmbeddingProfile.From(embeddingOptions.Value);
        oversampleFactor = retrievalOptions.Value.Semantic.OversampleFactor;
        contextCollection = vectorStore.GetCollection<Guid, ContextVectorRecord>(
            VectorCollectionNamePolicy.CreateContextCollectionName(profile.Provider, profile.Model, profile.Dimensions),
            VectorCollectionDefinitions.CreateContext(profile.Dimensions));
        documentCollection = vectorStore.GetCollection<Guid, DocumentVectorRecord>(
            VectorCollectionNamePolicy.CreateDocumentCollectionName(profile.Provider, profile.Model, profile.Dimensions),
            VectorCollectionDefinitions.CreateDocument(profile.Dimensions));
    }

    public async Task<IReadOnlyList<SemanticContextCandidateRecord>> FindContextCandidatesAsync(
        SemanticCandidateQuery query,
        ReadOnlyMemory<float> queryVector,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        if (HasEmptyScope(query))
        {
            return [];
        }

        var vectorCandidates = await SearchContextVectorsAsync(query, queryVector, cancellationToken);
        if (vectorCandidates.Count == 0)
        {
            return [];
        }

        var ids = vectorCandidates.Select(candidate => candidate.Id).Distinct().ToArray();
        var sourceQuery = db.ContextItems
            .AsNoTracking()
            .Where(x => x.DepotId == query.DepotId &&
                        x.Status == ContextStatus.Active &&
                        (x.ExpiresAt == null || x.ExpiresAt > query.Now) &&
                        ids.Contains(x.Id));
        var workspaceIds = query.WorkspaceIds?.ToArray();
        var kinds = query.Kinds?.ToArray();
        if (workspaceIds is not null)
        {
            sourceQuery = sourceQuery.Where(x => workspaceIds.Contains(x.WorkspaceId));
        }

        if (kinds is not null)
        {
            sourceQuery = sourceQuery.Where(x => kinds.Contains(x.Kind));
        }

        var source = await sourceQuery
            .Select(x => new BootstrapContextCandidate(
                x.Id,
                x.DepotId,
                x.WorkspaceId,
                x.Kind,
                x.Key,
                x.Title,
                x.Content,
                x.TagsJson,
                x.Importance,
                x.Confidence,
                x.Status,
                x.VerificationStatus,
                x.ProvenanceTrust,
                x.SourceType,
                x.SourceAgent,
                x.SourceRef,
                x.SupersedesId,
                x.ExpiresAt,
                x.CreatedAt,
                x.UpdatedAt,
                x.MetadataJson))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return vectorCandidates
            .Where(candidate => source.ContainsKey(candidate.Id))
            .Select(candidate => new SemanticContextCandidateRecord(source[candidate.Id], candidate.Similarity))
            .Take(query.TopK)
            .ToArray();
    }

    public async Task<IReadOnlyList<SemanticDocumentCandidateRecord>> FindDocumentCandidatesAsync(
        SemanticCandidateQuery query,
        ReadOnlyMemory<float> queryVector,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        if (HasEmptyScope(query))
        {
            return [];
        }

        var vectorCandidates = await SearchDocumentVectorsAsync(query, queryVector, cancellationToken);
        if (vectorCandidates.Count == 0)
        {
            return [];
        }

        var ids = vectorCandidates.Select(candidate => candidate.Id).Distinct().ToArray();
        var sourceQuery = db.DocumentChunks
            .AsNoTracking()
            .Where(x => x.DepotId == query.DepotId &&
                        x.Document != null &&
                        x.Document.Status == DocumentStatus.Active &&
                        x.Document.IndexStatus == DocumentIndexStatus.Indexed &&
                        ids.Contains(x.Id));
        var workspaceIds = query.WorkspaceIds?.ToArray();
        if (workspaceIds is not null)
        {
            sourceQuery = sourceQuery.Where(x => workspaceIds.Contains(x.WorkspaceId));
        }

        var source = await sourceQuery
            .Select(x => new BootstrapDocumentChunkCandidate(
                x.Id,
                x.DepotId,
                x.DocumentId,
                x.WorkspaceId,
                x.Ordinal,
                x.Document!.Path,
                x.Document.Title,
                x.HeadingPath,
                x.Content,
                x.ContentHash,
                x.UpdatedAt))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return vectorCandidates
            .Where(candidate => source.ContainsKey(candidate.Id))
            .Select(candidate => new SemanticDocumentCandidateRecord(source[candidate.Id], candidate.Similarity))
            .Take(query.TopK)
            .ToArray();
    }

    private async Task<IReadOnlyList<(Guid Id, double Similarity)>> SearchContextVectorsAsync(
        SemanticCandidateQuery query,
        ReadOnlyMemory<float> queryVector,
        CancellationToken cancellationToken)
    {
        try
        {
            var candidates = new List<(Guid Id, double Similarity)>();
            await foreach (var result in contextCollection.SearchAsync(
                               queryVector,
                               CandidateLimit(query.TopK),
                               new VectorSearchOptions<ContextVectorRecord>
                               {
                                   Filter = BuildContextFilter(query)
                               },
                               cancellationToken))
            {
                candidates.Add((result.Record.ContextItemId, NormalizeSimilarity(result.Score)));
            }

            return candidates;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "{ErrorCode} prevented semantic context search.", ApplicationErrorCodes.VectorSearchFailed);
            throw new ContextDepotApplicationException(ApplicationErrorCodes.VectorSearchFailed);
        }
    }

    private async Task<IReadOnlyList<(Guid Id, double Similarity)>> SearchDocumentVectorsAsync(
        SemanticCandidateQuery query,
        ReadOnlyMemory<float> queryVector,
        CancellationToken cancellationToken)
    {
        try
        {
            var candidates = new List<(Guid Id, double Similarity)>();
            await foreach (var result in documentCollection.SearchAsync(
                               queryVector,
                               CandidateLimit(query.TopK),
                               new VectorSearchOptions<DocumentVectorRecord>
                               {
                                   Filter = BuildDocumentFilter(query)
                               },
                               cancellationToken))
            {
                candidates.Add((result.Record.DocumentChunkId, NormalizeSimilarity(result.Score)));
            }

            return candidates;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "{ErrorCode} prevented semantic document search.", ApplicationErrorCodes.VectorSearchFailed);
            throw new ContextDepotApplicationException(ApplicationErrorCodes.VectorSearchFailed);
        }
    }

    private int CandidateLimit(int topK) => checked(topK * oversampleFactor);

    private static Expression<Func<ContextVectorRecord, bool>> BuildContextFilter(SemanticCandidateQuery query)
    {
        var depotId = query.DepotId;
        var workspaceIds = query.WorkspaceIds?.ToArray();
        var kinds = query.Kinds?.Select(kind => kind.ToString().ToLowerInvariant()).ToArray();
        if (workspaceIds is not null && kinds is not null)
        {
            return record => record.DepotId == depotId &&
                             workspaceIds.Contains(record.WorkspaceId) &&
                             kinds.Contains(record.Kind);
        }

        if (workspaceIds is not null)
        {
            return record => record.DepotId == depotId && workspaceIds.Contains(record.WorkspaceId);
        }

        if (kinds is not null)
        {
            return record => record.DepotId == depotId && kinds.Contains(record.Kind);
        }

        return record => record.DepotId == depotId;
    }

    private static Expression<Func<DocumentVectorRecord, bool>> BuildDocumentFilter(SemanticCandidateQuery query)
    {
        var depotId = query.DepotId;
        var workspaceIds = query.WorkspaceIds?.ToArray();
        if (workspaceIds is not null)
        {
            return record => record.DepotId == depotId && workspaceIds.Contains(record.WorkspaceId);
        }

        return record => record.DepotId == depotId;
    }

    private static bool HasEmptyScope(SemanticCandidateQuery query) =>
        query.WorkspaceIds is { Count: 0 } || query.Kinds is { Count: 0 };

    private static double NormalizeSimilarity(double? score) => Math.Clamp(score ?? 0, 0, 1);

    private static void ValidateQuery(SemanticCandidateQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.TopK <= 0 || query.DepotId == Guid.Empty)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidSearchQuery);
        }
    }
}
