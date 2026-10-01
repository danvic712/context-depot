using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Retrieval;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Workspaces.Contracts;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application.Overview;

public sealed record KnowledgePreview(Guid Id, string Type, string Title, string Workspace,
    string Content, DateTimeOffset UpdatedAt);

public sealed class KnowledgeSearchAppService(
    IContextQueryAppService contexts,
    IDocumentAppService documents,
    IWorkspaceAppService workspaces,
    IOptionsMonitor<RetrievalOptions> options)
{
    public Task<ContextSearchResult> SearchAsync(string query, string? workspace, CancellationToken cancellationToken) =>
        contexts.SearchAsync(new ContextSearchRequest(query,
            Workspaces: string.IsNullOrWhiteSpace(workspace) ? null : [workspace],
            Limit: Math.Min(50, options.CurrentValue.Search.MaxLimit)), cancellationToken);

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
