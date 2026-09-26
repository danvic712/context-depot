namespace ContextDepot.Application.Workspaces;

public sealed record WorkspaceTreeNode(Guid Id, Guid? ParentWorkspaceId, string Slug);

public sealed class WorkspaceTopology
{
    private readonly Dictionary<Guid, WorkspaceTreeNode> nodes;
    private readonly Dictionary<Guid, string> paths;
    private readonly Dictionary<string, Guid> idsByPath;
    private readonly Dictionary<Guid, List<Guid>> children;

    public WorkspaceTopology(IEnumerable<WorkspaceTreeNode> workspaceNodes)
    {
        ArgumentNullException.ThrowIfNull(workspaceNodes);
        nodes = workspaceNodes.ToDictionary(node => node.Id);
        paths = new Dictionary<Guid, string>(nodes.Count);
        children = new Dictionary<Guid, List<Guid>>();
        foreach (var node in nodes.Values)
        {
            if (node.ParentWorkspaceId is Guid parentId)
            {
                if (!children.TryGetValue(parentId, out var siblings))
                {
                    siblings = [];
                    children[parentId] = siblings;
                }

                siblings.Add(node.Id);
            }

            BuildPath(node);
        }

        idsByPath = paths.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<Guid, string> Paths => paths;

    public bool TryGetId(string path, out Guid id) => idsByPath.TryGetValue(path, out id);

    public bool TryGetPath(Guid id, out string path) => paths.TryGetValue(id, out path!);

    public IReadOnlySet<Guid> AncestorsIncludingSelf(IEnumerable<Guid> workspaceIds)
    {
        ArgumentNullException.ThrowIfNull(workspaceIds);
        var result = new HashSet<Guid>(workspaceIds);
        foreach (var workspaceId in result.ToArray())
        {
            var currentId = workspaceId;
            var visited = new HashSet<Guid>();
            while (visited.Add(currentId) && nodes.TryGetValue(currentId, out var node) &&
                   node.ParentWorkspaceId is Guid parentId)
            {
                result.Add(parentId);
                currentId = parentId;
            }
        }

        return result;
    }

    public IReadOnlyList<Guid> AccessibleDescendants(Guid rootId, Func<Guid, bool> canAccess)
    {
        ArgumentNullException.ThrowIfNull(canAccess);
        var result = new List<Guid>();
        var pending = new Queue<Guid>([rootId]);
        var visited = new HashSet<Guid> { rootId };
        while (pending.TryDequeue(out var parentId))
        {
            if (!children.TryGetValue(parentId, out var childIds))
            {
                continue;
            }

            foreach (var childId in childIds)
            {
                if (visited.Add(childId) && canAccess(childId))
                {
                    result.Add(childId);
                    pending.Enqueue(childId);
                }
            }
        }

        return result;
    }

    private string BuildPath(WorkspaceTreeNode workspace)
    {
        if (paths.TryGetValue(workspace.Id, out var knownPath))
        {
            return knownPath;
        }

        var chain = new List<WorkspaceTreeNode>();
        var visited = new HashSet<Guid>();
        var current = workspace;
        var prefix = string.Empty;
        while (visited.Add(current.Id))
        {
            if (paths.TryGetValue(current.Id, out var cachedPrefix))
            {
                prefix = cachedPrefix;
                break;
            }

            chain.Add(current);
            if (current.ParentWorkspaceId is not Guid parentId || !nodes.TryGetValue(parentId, out current!))
            {
                break;
            }
        }

        for (var index = chain.Count - 1; index >= 0; index--)
        {
            prefix = prefix.Length == 0 ? chain[index].Slug : prefix + "/" + chain[index].Slug;
            paths[chain[index].Id] = prefix;
        }

        return paths[workspace.Id];
    }
}
