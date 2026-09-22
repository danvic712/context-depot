using Microsoft.Extensions.Options;
using ContextDepot.Application.Embeddings;

namespace ContextDepot.Infrastructure.VectorStore;

public sealed class VectorCollectionInitializer(
    PostgreSqlVectorStore vectorStore,
    IOptions<EmbeddingOptions> embeddingOptions)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var configuration = embeddingOptions.Value;
        var contextName = VectorCollectionNamePolicy.CreateContextCollectionName(
            configuration.Provider,
            configuration.Model,
            configuration.Dimensions);
        var documentName = VectorCollectionNamePolicy.CreateDocumentCollectionName(
            configuration.Provider,
            configuration.Model,
            configuration.Dimensions);

        var contextCollection = vectorStore.GetCollection<Guid, ContextVectorRecord>(
            contextName,
            VectorCollectionDefinitions.CreateContext(configuration.Dimensions));
        var documentCollection = vectorStore.GetCollection<Guid, DocumentVectorRecord>(
            documentName,
            VectorCollectionDefinitions.CreateDocument(configuration.Dimensions));

        await contextCollection.EnsureCollectionExistsAsync(cancellationToken);
        await documentCollection.EnsureCollectionExistsAsync(cancellationToken);
    }
}
