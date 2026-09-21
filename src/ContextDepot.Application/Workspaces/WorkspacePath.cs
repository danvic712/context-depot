using System.Text.RegularExpressions;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Workspaces;

public static partial class WorkspacePath
{
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidWorkspacePath);
        }

        var segments = path.Split('/', StringSplitOptions.None);
        if (segments.Length == 0 || segments.Any(segment => string.IsNullOrWhiteSpace(segment) || !SlugRegex().IsMatch(segment)))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidWorkspacePath);
        }

        return string.Join('/', segments);
    }

    public static IReadOnlyList<string> Segments(string normalizedPath) => normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

    public static string BuildPath(IReadOnlyDictionary<Guid, Workspace> workspaces, Workspace workspace)
        => BuildPath(workspaces, workspace, new Dictionary<Guid, string>());

    public static IReadOnlyDictionary<Guid, string> BuildPaths(IEnumerable<Workspace> workspaces)
    {
        var byId = workspaces.ToDictionary(x => x.Id);
        var paths = new Dictionary<Guid, string>(byId.Count);
        foreach (var workspace in byId.Values)
        {
            BuildPath(byId, workspace, paths);
        }

        return paths;
    }

    private static string BuildPath(
        IReadOnlyDictionary<Guid, Workspace> workspaces,
        Workspace workspace,
        IDictionary<Guid, string> paths)
    {
        if (paths.TryGetValue(workspace.Id, out var knownPath))
        {
            return knownPath;
        }

        var chain = new List<Workspace>();
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
            if (current.ParentWorkspaceId is not Guid parentId || !workspaces.TryGetValue(parentId, out current!))
            {
                prefix = string.Empty;
                break;
            }
        }

        for (var index = chain.Count - 1; index >= 0; index--)
        {
            prefix = prefix.Length == 0 ? chain[index].Slug : chain[index].Slug + "/" + prefix;
            paths[chain[index].Id] = prefix;
        }

        return paths[workspace.Id];
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}
