using System.Text.RegularExpressions;
using ContextDepot.Application.Abstractions;

namespace ContextDepot.Application.Workspaces;

public static partial class WorkspacePath
{
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ContextDepotApplicationException("InvalidWorkspacePath", "Workspace path is required.");
        }

        var segments = path.Split('/', StringSplitOptions.None);
        if (segments.Length == 0 || segments.Any(segment => string.IsNullOrWhiteSpace(segment) || !SlugRegex().IsMatch(segment)))
        {
            throw new ContextDepotApplicationException("InvalidWorkspacePath", "Workspace path must contain lowercase kebab-case segments.");
        }

        return string.Join('/', segments);
    }

    public static IReadOnlyList<string> Segments(string normalizedPath) => normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}
