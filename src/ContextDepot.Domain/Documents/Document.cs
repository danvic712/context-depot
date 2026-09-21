using ContextDepot.Domain.Workspaces;
using ContextDepot.Domain.Documents.Enums;

namespace ContextDepot.Domain.Documents;

public sealed class Document
{
    private Document()
    {
    }

    public Document(Guid id, Guid depotId, Guid workspaceId, string path, string title, DateTimeOffset now)
    {
        Id = id;
        DepotId = depotId;
        WorkspaceId = workspaceId;
        Path = path;
        Title = title;
        Status = DocumentStatus.Active;
        IndexStatus = DocumentIndexStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid DepotId { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Path { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string ContentHash { get; private set; } = string.Empty;

    public string? IndexedContentHash { get; private set; }

    public DocumentStatus Status { get; private set; }

    public DocumentIndexStatus IndexStatus { get; private set; }

    public string? LastIndexError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Workspace? Workspace { get; private set; }

    public ICollection<DocumentChunk> Chunks { get; } = new List<DocumentChunk>();

    public void UpdateMetadata(string title, string contentHash, DocumentIndexStatus indexStatus, DateTimeOffset now)
    {
        Title = title;
        ContentHash = contentHash;
        IndexStatus = indexStatus;
        UpdatedAt = now;
        if (indexStatus == DocumentIndexStatus.Indexed)
        {
            IndexedContentHash = contentHash;
            LastIndexError = null;
        }
    }

    public void Reconcile(string title, string contentHash, DateTimeOffset now)
    {
        Title = title;
        ContentHash = contentHash;
        IndexedContentHash = contentHash;
        Status = DocumentStatus.Active;
        IndexStatus = DocumentIndexStatus.Indexed;
        LastIndexError = null;
        UpdatedAt = now;
    }

    public void MarkPending(DateTimeOffset now)
    {
        IndexStatus = DocumentIndexStatus.Pending;
        UpdatedAt = now;
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        IndexStatus = DocumentIndexStatus.Failed;
        LastIndexError = error;
        UpdatedAt = now;
    }

    public void MarkArchived(DateTimeOffset now)
    {
        Status = DocumentStatus.Archived;
        IndexStatus = DocumentIndexStatus.Pending;
        UpdatedAt = now;
    }
}
