using ContextDepot.Application.Shared.Exceptions;
using System.Globalization;
using System.Text;

namespace ContextDepot.Application.Documents;

public static class DocumentPath
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    /// <summary>
    /// Resolves a URL-encoded Markdown link against its source directory within one workspace.
    /// A leading slash refers to the workspace root; traversal beyond that root is rejected.
    /// </summary>
    public static string ResolveLink(string sourcePath, string linkPath) => Normalize(ResolveLinkPath(sourcePath, linkPath));

    internal static string ResolveLinkPath(string sourcePath, string linkPath)
    {
        var path = DecodeLinkComponent(linkPath);
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith("//", StringComparison.Ordinal) ||
            linkPath.Contains('?') || linkPath.Contains('#') || path.Contains('\\') || path.Contains(':') ||
            path.Any(char.IsControl))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidDocumentPath);

        var segments = path.StartsWith('/')
            ? new List<string>()
            : Normalize(sourcePath).Split('/').SkipLast(1).ToList();
        foreach (var segment in path.Split('/'))
        {
            if (segment is "" or ".") continue;
            if (segment == "..")
            {
                if (segments.Count == 0)
                    throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidDocumentPath);
                segments.RemoveAt(segments.Count - 1);
            }
            else segments.Add(segment);
        }
        return string.Join('/', segments);
    }

    internal static string DecodeLinkComponent(string value)
    {
        var bytes = new List<byte>();
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%') continue;
            bytes.Clear();
            while (index < value.Length && value[index] == '%')
            {
                if (index + 2 >= value.Length || !Uri.IsHexDigit(value[index + 1]) || !Uri.IsHexDigit(value[index + 2]))
                    throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidDocumentPath);
                bytes.Add(byte.Parse(value.AsSpan(index + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                index += 3;
            }
            try { StrictUtf8.GetCharCount(bytes.ToArray()); }
            catch (DecoderFallbackException)
            {
                throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidDocumentPath);
            }
            index--;
        }
        return Uri.UnescapeDataString(value);
    }

    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidDocumentPath);
        }

        var normalized = path.Replace('\\', '/').Trim();
        var segments = normalized.Split('/', StringSplitOptions.None);
        if (normalized.StartsWith('/') || !normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
            segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".." ||
                segment.Contains(':') || segment.Any(char.IsControl)))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidDocumentPath);
        }

        return string.Join('/', segments);
    }
}
