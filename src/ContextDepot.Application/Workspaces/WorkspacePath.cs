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
        => new WorkspaceTopology(workspaces.Values.Select(ToNode)).Paths[workspace.Id];

    public static IReadOnlyDictionary<Guid, string> BuildPaths(IEnumerable<Workspace> workspaces)
    {
        return new WorkspaceTopology(workspaces.Select(ToNode)).Paths;
    }

    private static WorkspaceTreeNode ToNode(Workspace workspace) =>
        new(workspace.Id, workspace.ParentWorkspaceId, workspace.Slug);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}
