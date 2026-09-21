using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Depots;

namespace ContextDepot.Domain.Workspaces;

public sealed class Workspace
{
    private Workspace()
    {
    }

    public Workspace(Guid id, Guid depotId, Guid? parentWorkspaceId, string name, string slug, string? description, DateTimeOffset now)
    {
        Id = id;
        DepotId = depotId;
        ParentWorkspaceId = parentWorkspaceId;
        Name = name;
        Slug = slug;
        Description = description;
        CreatedAt = now;
        UpdatedAt = now;
        MetadataJson = "{}";
    }

    public Guid Id { get; private set; }

    public Guid DepotId { get; private set; }

    public Guid? ParentWorkspaceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string MetadataJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Depot? Depot { get; private set; }

    public Workspace? ParentWorkspace { get; private set; }

    public ICollection<Workspace> Children { get; } = new List<Workspace>();

    public ICollection<ContextItem> ContextItems { get; } = new List<ContextItem>();

    public ICollection<Document> Documents { get; } = new List<Document>();

    public ICollection<WorkspaceAccessGrant> AccessKeyGrants { get; } = new List<WorkspaceAccessGrant>();

    public void Update(string name, string? description, string metadataJson, DateTimeOffset now)
    {
        Name = name;
        Description = description;
        MetadataJson = metadataJson;
        UpdatedAt = now;
    }
}
