using ContextDepot.Infrastructure.RuntimeConfiguration;

namespace ContextDepot.Infrastructure.VectorStore;

public sealed class VectorCollectionInitializer(
    PostgreSqlVectorStore vectorStore,
    InferenceRuntimeSnapshotAccessor snapshotAccessor)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var embedding = snapshotAccessor.Current.Embedding;
        if (embedding is null)
        {
            return;
        }

        var contextName = VectorCollectionNamePolicy.CreateContextCollectionName(
            embedding.ProviderName,
            embedding.ModelName,
            embedding.Dimensions);
        var documentName = VectorCollectionNamePolicy.CreateDocumentCollectionName(
            embedding.ProviderName,
            embedding.ModelName,
            embedding.Dimensions);

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
