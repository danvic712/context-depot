using ContextDepot.Infrastructure.RuntimeConfiguration;

namespace ContextDepot.Infrastructure.VectorStore;

public sealed class VectorCollectionInitializer(
    PostgreSqlVectorStore vectorStore)
{
    public async Task InitializeAsync(EmbeddingRouteRuntimeSnapshot? embedding, CancellationToken cancellationToken)
    {
        if (embedding is null)
        {
            return;
        }

        var contextName = VectorCollectionNamePolicy.CreateContextCollectionName(embedding.ProfileFingerprint);
        var documentName = VectorCollectionNamePolicy.CreateDocumentCollectionName(embedding.ProfileFingerprint);

        var contextCollection = vectorStore.GetCollection<Guid, ContextVectorRecord>(
            contextName,
            VectorCollectionDefinitions.CreateContext(embedding.Dimensions));
        var documentCollection = vectorStore.GetCollection<Guid, DocumentVectorRecord>(
            documentName,
            VectorCollectionDefinitions.CreateDocument(embedding.Dimensions));

        await contextCollection.EnsureCollectionExistsAsync(cancellationToken);
        await documentCollection.EnsureCollectionExistsAsync(cancellationToken);
    }
}
