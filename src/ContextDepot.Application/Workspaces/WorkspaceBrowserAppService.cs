using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;

namespace ContextDepot.Application.Workspaces;

public sealed class WorkspaceBrowserAppService(
    ICurrentDepotContext currentDepot,
    IWorkspaceBrowserRepository repository,
    TimeProvider timeProvider)
{
    public async Task<WorkspaceDirectory> BrowseAsync(Guid? parentId, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 60)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidResourceQuery);
        var now = timeProvider.GetUtcNow();
        if (parentId is Guid id && await repository.GetAsync(currentDepot.DepotId, id, now, cancellationToken) is null)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);
        return await repository.BrowseAsync(currentDepot.DepotId, parentId, page, pageSize, now, cancellationToken);
    }

    public async Task<WorkspaceDetail> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetAsync(currentDepot.DepotId, id, timeProvider.GetUtcNow(), cancellationToken)
        ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);

    public async Task<WorkspaceKnowledge> ListKnowledgeAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 60)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidResourceQuery);
        return await repository.ListKnowledgeAsync(currentDepot.DepotId, id, page, pageSize,
            timeProvider.GetUtcNow(), cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);
    }
}
