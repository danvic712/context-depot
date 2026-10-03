using ContextDepot.Application.Workspaces.Dtos;

namespace ContextDepot.Application.Workspaces.Contracts;

public interface IWorkspaceBrowserRepository
{
    Task<WorkspaceDirectory> BrowseAsync(Guid depotId, Guid? parentId, int page, int pageSize,
        DateTimeOffset asOf, CancellationToken cancellationToken);
    Task<WorkspaceDetail?> GetAsync(Guid depotId, Guid id, DateTimeOffset asOf, CancellationToken cancellationToken);
    Task<WorkspaceKnowledge?> ListKnowledgeAsync(Guid depotId, Guid id, int page, int pageSize,
        DateTimeOffset asOf, CancellationToken cancellationToken);
}
