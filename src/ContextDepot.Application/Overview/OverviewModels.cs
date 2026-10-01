using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Documents.Enums;

namespace ContextDepot.Application.Overview;

public sealed record ResourceCollection<T>(DateTimeOffset AsOf, IReadOnlyList<T> Items, bool HasMore);
public sealed record WorkspaceSummary(Guid Id, string Name, string? Description, string Path,
    int ContextCount, int DocumentCount, DateTimeOffset ActivityAt);
public sealed record KnowledgeWorkspace(Guid Id, string Path);
public sealed record KnowledgeSummary(Guid Id, string Type, ContextKind? Kind, string Title,
    KnowledgeWorkspace Workspace, DateTimeOffset UpdatedAt, DocumentIndexStatus? IndexStatus);
public sealed record CreateWorkspaceRequest(string? Name, string? Path, string? Description);

public interface IOverviewRepository
{
    Task<ResourceCollection<WorkspaceSummary>> ListWorkspacesAsync(Guid depotId, int limit, DateTimeOffset asOf, CancellationToken cancellationToken);
    Task<ResourceCollection<KnowledgeSummary>> ListKnowledgeAsync(Guid depotId, int limit, DateTimeOffset asOf, CancellationToken cancellationToken);
    // False means the root slug already exists. Never update an existing workspace.
    Task<bool> CreateWorkspaceAsync(Guid depotId, Guid id, string name, string path, string? description, DateTimeOffset now, CancellationToken cancellationToken);
}
