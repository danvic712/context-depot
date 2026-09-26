using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces;

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

        var workspaceTopology = await workspaceAppService.LoadTopologyAsync(cancellationToken);
        var ids = new HashSet<Guid>();
        var paths = new Dictionary<Guid, string>();
        foreach (var path in request.Workspaces)
        {
            var normalizedPath = WorkspacePath.Normalize(path);
            if (!workspaceTopology.TryGetId(normalizedPath, out var workspaceId) ||
                !workspaceAccess.CanAccess(workspaceId))
            {
                throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);
            }

            ids.Add(workspaceId);
            paths[workspaceId] = normalizedPath;
            if (request.IncludeDescendants)
            {
                foreach (var descendantId in workspaceTopology.AccessibleDescendants(workspaceId, workspaceAccess.CanAccess))
                {
                    ids.Add(descendantId);
                    paths[descendantId] = workspaceTopology.Paths[descendantId];
                }
            }
        }

        return (ids, paths);
    }

    public async Task AddMissingPathsAsync(
        IEnumerable<Guid> workspaceIds,
        IDictionary<Guid, string> workspacePaths,
        CancellationToken cancellationToken)
    {
        var missing = workspaceIds.Distinct().ToArray();
        if (missing.Length == 0)
        {
            return;
        }

        var workspaceTopology = await workspaceAppService.LoadTopologyAsync(cancellationToken);
        foreach (var workspaceId in missing)
        {
            if (workspaceAccess.CanAccess(workspaceId) &&
                workspaceTopology.TryGetPath(workspaceId, out var path))
            {
                workspacePaths[workspaceId] = path;
            }
        }
    }

}
