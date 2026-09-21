namespace ContextDepot.Domain.Documents;

public sealed class DocumentChunk
{
    private DocumentChunk()
    {
    }

    public DocumentChunk(Guid id, Guid depotId, Guid documentId, Guid workspaceId, int ordinal, string headingPath, string content, string contentHash, DateTimeOffset now)
    {
        Id = id;
        DepotId = depotId;
        DocumentId = documentId;
        WorkspaceId = workspaceId;
        Ordinal = ordinal;
        HeadingPath = headingPath;
        Content = content;
        ContentHash = contentHash;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid DepotId { get; private set; }

    public Guid DocumentId { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public int Ordinal { get; private set; }

    public string HeadingPath { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public string ContentHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Document? Document { get; private set; }
}
