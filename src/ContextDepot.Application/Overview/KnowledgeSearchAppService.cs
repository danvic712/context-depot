using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Retrieval;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Workspaces.Contracts;
using Microsoft.Extensions.Options;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Overview;

/// <summary>
/// Searches authorized knowledge and reads canonical content for search and workspace readers.
/// </summary>
/// <param name="contexts">
/// The context service that enforces retrieval authorization and relevance ordering.
/// </param>
/// <param name="documents">
/// The document service used to load authorized canonical Markdown.
/// </param>
/// <param name="workspaces">
/// The workspace service used to resolve visible search scopes.
/// </param>
/// <param name="options">
/// The current retrieval limits.
/// </param>
public sealed class KnowledgeSearchAppService(
    IContextQueryAppService contexts,
    IDocumentAppService documents,
    IWorkspaceAppService workspaces,
    IOptionsMonitor<RetrievalOptions> options)
{
    /// <summary>
    /// Finds distinct contexts and documents in retrieval relevance order within the caller's workspace grants.
    /// </summary>
    /// <param name="query">
    /// The search text passed to authorized retrieval.
    /// </param>
    /// <param name="workspace">
    /// An optional workspace path; blank values do not restrict the search scope.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <param name="kind">
    /// An optional context kind name, matched without case sensitivity; selecting a kind excludes documents.
    /// </param>
    /// <returns>
    /// A task whose result contains distinct ranked hits, the effective limit and the retrieval degradation indicator.
    /// </returns>
    /// <exception cref="ContextDepotApplicationException">
    /// The context kind or retrieval request is invalid, or the requested scope is not authorized.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before completion.
    /// </exception>
    public async Task<KnowledgeSearchResponse> SearchAsync(string query, string? workspace, CancellationToken cancellationToken,
        string? kind = null)
    {
        ContextKind? selectedKind = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            if (!Enum.TryParse<ContextKind>(kind, true, out var parsed) || !Enum.IsDefined(parsed) ||
                !string.Equals(parsed.ToString(), kind, StringComparison.OrdinalIgnoreCase))
                throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidSearchQuery);
            selectedKind = parsed;
        }
        var limit = Math.Min(50, options.CurrentValue.Search.MaxLimit);
        var result = await contexts.SearchAsync(new ContextSearchRequest(query,
            Workspaces: string.IsNullOrWhiteSpace(workspace) ? null : [workspace],
            Kinds: selectedKind is null ? null : [selectedKind.Value],
            Limit: limit,
            IncludeDocuments: selectedKind is null,
            IncludeResultOrder: true), cancellationToken);
        var hits = result.Contexts.Select(item => (item.Ordinal, Hit: new KnowledgeSearchHit(item.ContextId, "context",
                string.IsNullOrWhiteSpace(item.Title) ? item.Key ?? item.Kind.ToString() : item.Title,
                item.Workspace, item.Content, item.Kind.ToString().ToLowerInvariant())))
            .Concat(result.Documents.Select(item => (item.Ordinal, Hit: new KnowledgeSearchHit(item.DocumentId, "document",
                string.IsNullOrWhiteSpace(item.Title) ? item.Path : item.Title, item.Workspace, item.Excerpt, "document"))))
            .OrderBy(item => item.Ordinal)
            .Select(item => item.Hit)
            .DistinctBy(item => (item.Type, item.Id))
            .ToArray();
        return new(hits, result.Retrieval.RetrievalDegraded, limit);
    }

    /// <summary>
    /// Lists only workspaces visible to the current caller for search scope selection.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task whose result contains readable workspace identifiers, names and full paths, ordered by path.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before completion.
    /// </exception>
    public async Task<IReadOnlyList<KnowledgeSearchWorkspace>> ListWorkspacesAsync(CancellationToken cancellationToken)
    {
        var visible = await workspaces.ListVisibleAsync(cancellationToken);
        return visible.OrderBy(workspace => workspace.Path, StringComparer.Ordinal)
            .Select(workspace => new KnowledgeSearchWorkspace(workspace.Id, workspace.Name, workspace.Path)).ToArray();
    }

    /// <summary>
    /// Reads canonical knowledge content and optionally restricts it to the requested workspace.
    /// </summary>
    /// <param name="type">
    /// The resource type: context or document.
    /// </param>
    /// <param name="id">
    /// The resource identifier.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <param name="workspaceId">
    /// An optional owning-workspace constraint; mismatches are reported as not found.
    /// </param>
    /// <returns>
    /// A task whose result contains canonical content and its precise observed revision.
    /// </returns>
    /// <remarks>
    /// Resources outside the requested workspace are reported as not found using the same error as a missing resource.
    /// </remarks>
    /// <exception cref="ContextDepotApplicationException">
    /// The resource type is unsupported, the resource is missing, access is denied,
    /// or the owning workspace does not match the requested workspace.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before completion.
    /// </exception>
    public async Task<KnowledgePreview> GetAsync(string type, Guid id, CancellationToken cancellationToken, Guid? workspaceId = null)
    {
        if (type == "context")
        {
            var context = await contexts.GetAsync(id, cancellationToken)
                ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.ContextNotFound);
            if (workspaceId is not null && context.WorkspaceId != workspaceId)
                throw new ContextDepotApplicationException(ApplicationErrorCodes.ContextNotFound);
            var owner = await workspaces.GetAsync(context.WorkspaceId, cancellationToken);
            return new(context.Id, type, context.Title ?? context.Key ?? context.Kind.ToString(),
                context.Workspace, context.Content, context.UpdatedAt, owner?.Name);
        }

        if (type == "document")
        {
            var document = await documents.GetAsync(id, cancellationToken)
                ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.DocumentNotFound);
            if (workspaceId is not null && document.Metadata.WorkspaceId != workspaceId)
                throw new ContextDepotApplicationException(ApplicationErrorCodes.DocumentNotFound);
            var owner = await workspaces.GetAsync(document.Metadata.WorkspaceId, cancellationToken);
            return new(document.Metadata.Id, type,
                string.IsNullOrWhiteSpace(document.Metadata.Title) ? document.Metadata.Path : document.Metadata.Title,
                document.Metadata.Workspace, document.Content, document.Metadata.UpdatedAt, owner?.Name);
        }

        throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidResourceQuery);
    }
}
