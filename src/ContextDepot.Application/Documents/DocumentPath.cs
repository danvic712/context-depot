using ContextDepot.Application.Abstractions;

namespace ContextDepot.Application.Documents;

public static class DocumentPath
{
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
        {
            throw new ContextDepotApplicationException("InvalidDocumentPath", "Document path must be a relative Markdown path.");
        }

        var normalized = path.Replace('\\', '/').Trim();
        var segments = normalized.Split('/', StringSplitOptions.None);
        if (normalized.StartsWith('/') || !normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".." || segment.Contains('\0')))
        {
            throw new ContextDepotApplicationException("InvalidDocumentPath", "Document path must be a safe relative .md path.");
        }

        return string.Join('/', segments);
    }
}
