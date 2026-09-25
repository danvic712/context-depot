using System.ComponentModel;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.MCP.Shared;
using ModelContextProtocol.Server;

namespace ContextDepot.MCP.Contexts;

public sealed class ContextTools(ILogger<ContextTools> logger)
{
    [McpServerTool(Name = "context_bootstrap", Title = "Load task-aware context", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Load the current task's relevant Structured Context and Markdown excerpts in one call. Use this for ordinary task continuity; do not provide depotId.")]
    public async Task<BootstrapResult> BootstrapAsync(
        IContextBootstrapAppService service,
        [Description("A concise task-aware query describing what context is needed.")] string query,
        [Description("Optional explicit workspace paths. Omit to use conservative lexical auto scope.")] IReadOnlyList<string>? workspaces = null,
        [Description("Shared result token budget.")] int maxTokens = 1200,
        CancellationToken cancellationToken = default)
    {
        return await MCPToolExecutor.ExecuteAsync(
            () => service.BootstrapAsync(new BootstrapRequest(query, workspaces, maxTokens), cancellationToken),
            logger);
    }

    [McpServerTool(Name = "context_search", Title = "Search explicit context", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Search durable Context and Markdown excerpts for an explicit query. Omit workspaces to search every workspace available in the current access scope, or provide explicit workspace paths.")]
    public async Task<ContextSearchResult> SearchAsync(
        IContextQueryAppService service,
        [Description("The information you explicitly want to find.")] string query,
        [Description("Optional explicit workspace paths. Omit to search the current depot across all workspaces.")] IReadOnlyList<string>? workspaces = null,
        [Description("Include child workspaces below each explicit workspace.")] bool includeDescendants = false,
        [Description("Optional context kinds to include.")] IReadOnlyList<ContextKind>? kinds = null,
        [Description("Maximum number of Context and Markdown matches combined.")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return await MCPToolExecutor.ExecuteAsync(
            () => service.SearchAsync(
                new ContextSearchRequest(query, workspaces, includeDescendants, kinds, limit),
                cancellationToken),
            logger);
    }

    [McpServerTool(Name = "context_get", Title = "Get context details", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Read a known Context ID in the current depot scope, including archived and superseded lifecycle details. This never reads another depot and never changes lifecycle state.")]
    public async Task<ContextDetailModel?> GetAsync(
        IContextQueryAppService service,
        Guid contextId,
        CancellationToken cancellationToken = default)
    {
        return await MCPToolExecutor.ExecuteAsync(
            () => service.GetAsync(contextId, cancellationToken),
            logger);
    }

    [McpServerTool(Name = "context_save", Title = "Save durable context", Destructive = false, UseStructuredContent = true)]
    [Description("Save durable fact, preference, decision, goal, state, or event after the user explicitly asks to remember it or it is clearly long-lived. The current depot is configured by the host; never provide depotId.")]
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
        string? verificationStatus = null,
        string? sourceAgent = null,
        CancellationToken cancellationToken = default)
    {
        return await MCPToolExecutor.ExecuteAsync(async () =>
        {
            if (!Enum.TryParse<ContextKind>(kind, true, out var parsedKind))
            {
                throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidContextKind);
            }

            if (parsedKind == ContextKind.Observation)
            {
                throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidContextKind);
            }

            var parsedVerificationStatus = ParseVerificationStatus(verificationStatus);
            return await service.SaveAsync(new SaveContextCommand(workspace, parsedKind, content, key, title, tags, importance, confidence, supersedesId, parsedVerificationStatus, ProvenanceTrust.Unknown, SourceType.Agent, sourceAgent ?? "MCP", sourceRef), cancellationToken);
        }, logger);
    }

    private static VerificationStatus ParseVerificationStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return VerificationStatus.Unknown;
        }

        if (!Enum.TryParse<VerificationStatus>(value, true, out var parsed) || parsed == VerificationStatus.Verified)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidVerificationStatus);
        }

        return parsed;
    }

    [McpServerTool(Name = "context_archive", Title = "Archive context", Destructive = true, UseStructuredContent = true)]
    [Description("Archive a known Context ID in the current depot scope. Archived data is retained but excluded from ordinary bootstrap retrieval.")]
    public async Task<object> ArchiveAsync(IContextAppService service, Guid contextId, CancellationToken cancellationToken = default)
    {
        return await MCPToolExecutor.ExecuteAsync(async () =>
        {
            await service.ArchiveAsync(contextId, cancellationToken);
            return (object)new { archived = true, contextId };
        }, logger);
    }
}
