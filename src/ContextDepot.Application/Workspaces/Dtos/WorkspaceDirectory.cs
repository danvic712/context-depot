using ContextDepot.Application.Overview;

namespace ContextDepot.Application.Workspaces.Dtos;

public sealed record WorkspaceDirectoryItem(Guid Id, string Name, string? Description, string Path,
    int ContextCount, int DocumentCount, int SubspaceCount, DateTimeOffset ActivityAt);
public sealed record WorkspaceDirectory(DateTimeOffset AsOf, IReadOnlyList<WorkspaceDirectoryItem> Items,
    int TotalCount, int Page, int PageSize);
public sealed record WorkspaceAncestor(Guid Id, string Name, string Path);
public sealed record WorkspaceDetail(WorkspaceDirectoryItem Workspace, IReadOnlyList<WorkspaceAncestor> Ancestors);
public sealed record WorkspaceKnowledge(DateTimeOffset AsOf, IReadOnlyList<KnowledgeSummary> Items,
    int TotalCount, int Page, int PageSize);
