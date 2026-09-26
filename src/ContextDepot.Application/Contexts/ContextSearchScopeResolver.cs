using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;

namespace ContextDepot.Application.Contexts;

internal sealed class ContextSearchScopeResolver(
    IWorkspaceAccessContext workspaceAccess,
    IWorkspaceAppService workspaceAppService)
{
    public async Task<(HashSet<Guid>? Ids, Dictionary<Guid, string> Paths)> ResolveAsync(
        ContextSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Workspaces is not { Count: > 0 })
        {
            return workspaceAccess.HasUnrestrictedAccess
                ? (null, new Dictionary<Guid, string>())
                : (new HashSet<Guid>(workspaceAccess.WorkspaceIds), new Dictionary<Guid, string>());
        }

        var ids = new HashSet<Guid>();
        var paths = new Dictionary<Guid, string>();
        foreach (var path in request.Workspaces)
        {
            var workspace = await workspaceAppService.ResolveAsync(path, cancellationToken)
                ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);
            ids.Add(workspace.Id);
            paths[workspace.Id] = workspace.Path;
            if (request.IncludeDescendants)
            {
                await AddDescendantsAsync(workspace.Path, ids, paths, cancellationToken);
            }
        }

        return (ids, paths);
    }

    public async Task AddMissingPathsAsync(
        IEnumerable<Guid> workspaceIds,
        IDictionary<Guid, string> workspacePaths,
        CancellationToken cancellationToken)
    {
        foreach (var workspaceId in workspaceIds.Distinct())
        {
            var workspace = await workspaceAppService.GetAsync(workspaceId, cancellationToken);
            if (workspace is not null)
            {
                workspacePaths[workspaceId] = workspace.Path;
            }
        }
    }

    private async Task AddDescendantsAsync(
        string parentPath,
        ISet<Guid> ids,
        IDictionary<Guid, string> paths,
        CancellationToken cancellationToken)
    {
        var pending = new Queue<string>([parentPath]);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var currentPath = pending.Dequeue();
            if (!visited.Add(currentPath))
            {
                continue;
            }

            var children = await workspaceAppService.ListAsync(currentPath, cancellationToken);
            foreach (var child in children)
            {
                ids.Add(child.Id);
                paths[child.Id] = child.Path;
                pending.Enqueue(child.Path);
            }
        }
    }
}
