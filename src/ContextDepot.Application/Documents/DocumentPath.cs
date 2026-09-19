using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Application.Documents;

public static class DocumentPath
{
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidDocumentPath);
        }

        var normalized = path.Replace('\\', '/').Trim();
        var segments = normalized.Split('/', StringSplitOptions.None);
        if (normalized.StartsWith('/') || !normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".." || segment.Contains('\0')))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidDocumentPath);
        }

        return string.Join('/', segments);
    }
}
