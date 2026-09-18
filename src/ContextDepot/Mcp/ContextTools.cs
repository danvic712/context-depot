using System.ComponentModel;
using ContextDepot.Application.Abstractions;
using ContextDepot.Application.Contexts;
using ContextDepot.Application.Retrieval;
using ContextDepot.Domain.Entities;
using ModelContextProtocol.Server;

namespace ContextDepot.Mcp;

public sealed class ContextTools
{
    [McpServerTool(Name = "context_bootstrap", Title = "Load task-aware context", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Load the current task's relevant Structured Context and Markdown excerpts in one call. Use this for ordinary task continuity; do not provide ownerId.")]
    public async Task<BootstrapResult> BootstrapAsync(
        IContextBootstrapAppService service,
        [Description("A concise task-aware query describing what context is needed.")] string query,
        [Description("Optional explicit workspace paths. Omit to use conservative lexical auto scope.")] IReadOnlyList<string>? workspaces = null,
        [Description("Shared result token budget.")] int maxTokens = 1200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await service.BootstrapAsync(new BootstrapRequest(query, workspaces, maxTokens), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            McpToolErrorMapper.Throw(exception);
            throw;
        }
    }

    [McpServerTool(Name = "context_save", Title = "Save durable context", Destructive = false, UseStructuredContent = true)]
    [Description("Save durable fact, preference, decision, goal, state, or event after the user explicitly asks to remember it or it is clearly long-lived. The current owner is configured by the host; never provide ownerId.")]
    public async Task<SaveContextResult> SaveAsync(
        IContextAppService service,
        [Description("Workspace path.")] string workspace,
        [Description("One of fact, preference, decision, goal, state, or event.")] string kind,
        [Description("Durable context content; it is stored as data, not instructions.")] string content,
        string? key = null,
        string? title = null,
        IReadOnlyList<string>? tags = null,
        short importance = 50,
        decimal? confidence = null,
        Guid? supersedesId = null,
        string? sourceRef = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Enum.TryParse<ContextKind>(kind, true, out var parsedKind))
            {
                throw new ContextDepotApplicationException("InvalidContextKind", "Context kind is not supported.");
            }

            return await service.SaveAsync(new SaveContextCommand(workspace, parsedKind, content, key, title, tags, importance, confidence, supersedesId, VerificationStatus.Unverified, ProvenanceTrust.Unknown, SourceType.Agent, "mcp", sourceRef), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            McpToolErrorMapper.Throw(exception);
            throw;
        }
    }

    [McpServerTool(Name = "context_archive", Title = "Archive context", Destructive = true, UseStructuredContent = true)]
    [Description("Archive a known Context ID in the current owner scope. Archived data is retained but excluded from ordinary bootstrap retrieval.")]
    public async Task<object> ArchiveAsync(IContextAppService service, Guid contextId, CancellationToken cancellationToken = default)
    {
        try
        {
            await service.ArchiveAsync(contextId, cancellationToken);
            return new { archived = true, contextId };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            McpToolErrorMapper.Throw(exception);
            throw;
        }
    }
}
