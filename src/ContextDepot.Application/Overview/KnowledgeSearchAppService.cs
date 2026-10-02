using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Retrieval;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Workspaces.Contracts;
using Microsoft.Extensions.Options;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Overview;

public sealed record KnowledgePreview(Guid Id, string Type, string Title, string Workspace,
    string Content, DateTimeOffset UpdatedAt);

public sealed record KnowledgeSearchHit(Guid Id, string Type, string Title, string Workspace, string Excerpt, string Kind);
public sealed record KnowledgeSearchResponse(IReadOnlyList<KnowledgeSearchHit> Items, bool Degraded, int Limit);

public sealed class KnowledgeSearchAppService(
    IContextQueryAppService contexts,
    IDocumentAppService documents,
    IWorkspaceAppService workspaces,
    IOptionsMonitor<RetrievalOptions> options)
{
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

    public async Task<IReadOnlyList<KnowledgeWorkspace>> ListWorkspacesAsync(CancellationToken cancellationToken)
    {
        var topology = await workspaces.LoadTopologyAsync(cancellationToken);
        return topology.Paths.OrderBy(pair => pair.Value, StringComparer.Ordinal)
            .Select(pair => new KnowledgeWorkspace(pair.Key, pair.Value)).ToArray();
    }

    public async Task<KnowledgePreview> GetAsync(string type, Guid id, CancellationToken cancellationToken)
    {
        if (type == "context")
        {
            var context = await contexts.GetAsync(id, cancellationToken)
                ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.ContextNotFound);
            return new(context.Id, type, context.Title ?? context.Key ?? context.Kind.ToString(),
                context.Workspace, context.Content, context.UpdatedAt);
        }

        if (type == "document")
        {
            var document = await documents.GetAsync(id, cancellationToken)
                ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.DocumentNotFound);
            return new(document.Metadata.Id, type,
                string.IsNullOrWhiteSpace(document.Metadata.Title) ? document.Metadata.Path : document.Metadata.Title,
                document.Metadata.Workspace, document.Content, document.Metadata.UpdatedAt);
        }

        throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidResourceQuery);
    }
}
