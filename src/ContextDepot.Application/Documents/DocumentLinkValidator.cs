using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Documents.Enums;

namespace ContextDepot.Application.Documents;

/// <summary>
/// Validates one source document without following target links or fetching external URLs.
/// Callers establish workspace authorization before invoking this validator.
/// </summary>
public sealed class DocumentLinkValidator(IDocumentRepository repository, IMarkdownStore markdownStore)
{
    public async Task<DocumentLinkValidation> ValidateAsync(Guid depotId, Guid workspaceId, string workspace,
        string sourcePath, string content, CancellationToken cancellationToken)
    {
        var source = MarkdownDocumentAnalysis.Parse(content);
        var errors = new List<DocumentLinkDiagnostic>();
        var warnings = new List<DocumentLinkDiagnostic>();
        var targets = new Dictionary<string, (string? Code, HashSet<string>? Headings)>(StringComparer.Ordinal);
        foreach (var link in source.Links)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var href = link.Href;
            DocumentLinkDiagnostic Diagnostic(string code, string? targetPath = null, string? fragment = null) =>
                new(code, href, link.Line, link.Column, targetPath, fragment);

            // Match the reader's protocol allowlist before interpreting a local path.
            var colon = href.IndexOf(':');
            if (href.StartsWith("//", StringComparison.Ordinal) || colon >= 0 &&
                (href[..colon].All(character => char.IsAsciiLetterOrDigit(character) || character is '+' or '-' or '.')))
            {
                if (!Uri.TryCreate(href, UriKind.Absolute, out var external) ||
                    external.Scheme is not ("http" or "https") || string.IsNullOrEmpty(external.Host))
                    errors.Add(Diagnostic("invalid_protocol"));
                continue;
            }

            var hash = href.IndexOf('#');
            var beforeHash = hash < 0 ? href : href[..hash];
            var path = beforeHash.Split('?', 2)[0];
            string fragment;
            string decodedPath;
            try
            {
                decodedPath = DocumentPath.DecodeLinkComponent(path);
                fragment = DocumentPath.DecodeLinkComponent(hash < 0 ? string.Empty : href[(hash + 1)..]);
            }
            catch (ContextDepotApplicationException)
            {
                errors.Add(Diagnostic("invalid_path"));
                continue;
            }
            if (decodedPath.Length > 0 && !decodedPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                try { DocumentPath.ResolveLinkPath(sourcePath, path); }
                catch (ContextDepotApplicationException)
                {
                    errors.Add(Diagnostic("invalid_path"));
                    continue;
                }
                // Application routes and non-Markdown assets are outside document resolution.
                warnings.Add(Diagnostic("unsupported_local_link"));
                continue;
            }

            string targetPath;
            try
            {
                targetPath = path.Length == 0 ? sourcePath : DocumentPath.ResolveLink(sourcePath, path);
            }
            catch (ContextDepotApplicationException)
            {
                errors.Add(Diagnostic("invalid_path"));
                continue;
            }
            var headings = source.HeadingIds;
            if (!string.Equals(targetPath, sourcePath, StringComparison.Ordinal))
            {
                if (!targets.TryGetValue(targetPath, out var target))
                {
                    target = await ReadTargetAsync(depotId, workspaceId, workspace, targetPath, cancellationToken);
                    targets.Add(targetPath, target);
                }
                if (target.Code is not null)
                {
                    warnings.Add(Diagnostic(target.Code, targetPath, fragment.Length == 0 ? null : fragment));
                    continue;
                }
                headings = target.Headings!;
            }
            if (fragment.Length > 0 && !headings.Contains(fragment))
                warnings.Add(Diagnostic("anchor_not_found", targetPath, fragment));
        }
        return new(errors, warnings);
    }

    private async Task<(string? Code, HashSet<string>? Headings)> ReadTargetAsync(Guid depotId, Guid workspaceId,
        string workspace, string path, CancellationToken cancellationToken)
    {
        var target = await repository.GetByPathAsync(depotId, workspaceId, path, cancellationToken);
        if (target is null) return ("target_not_found", null);
        if (target.Status != DocumentStatus.Active) return ("target_archived", null);
        try
        {
            var canonical = await markdownStore.GetAsync(depotId, workspace + "/" + path, cancellationToken);
            return canonical is null ? ("target_content_unavailable", null) :
                (null, MarkdownDocumentAnalysis.Parse(canonical.Content).HeadingIds);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ("target_content_unavailable", null);
        }
    }
}
