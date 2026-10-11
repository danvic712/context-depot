using System.ComponentModel;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.AgentContext;
using ContextDepot.Application.AgentContext.Contracts;
using ContextDepot.Application.AgentContext.Dtos;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Contexts.Enums;
using ModelContextProtocol.Server;

namespace ContextDepot.MCP.Tools;

public sealed class ContextTools(MCPToolExecutor executor, ILogger<ContextTools> logger)
{
    [McpServerTool(Name = "context_bootstrap", Title = "Load task-aware context", ReadOnly = true, Idempotent = true,
        UseStructuredContent = true)]
    [Description(
        "Find information relevant to a task. Use event.type=taskInput for a submitted input. Returns the same context result for all callers and never creates a Workspace or saves knowledge.")]
    public async Task<AgentContextResult> BootstrapAsync(
        IAgentContextAppService service,
        [Description("Task query or submitted prompt. Automatic taskInput acknowledgements skip retrieval.")]
        string query,
        [Description("Optional explicit Workspace paths. Omit for conservative lexical auto scope.")]
        IReadOnlyList<string>? workspaces = null,
        [Description("Shared retrieval token budget, from 1 to 8000.")]
        int maxTokens = 1200,
        [Description("Optional stable project, topic, or personal subject. It identifies a target and does not grant access.")]
        WorkspaceSubject? subject = null,
        [Description("Optional event with type contextRequest or taskInput and input correlation IDs.")]
        AgentContextEvent? @event = null,
        [Description("Explicit workflow preferences. Capture and Workspace preparation default to disabled.")]
        AgentWorkflowPreferences? workflow = null,
        [Description("Optional language: zh-CN or en-US. Omit to use Accept-Language, then the default locale.")]
        string? locale = null,
        CancellationToken cancellationToken = default)
    {
        return await executor.ExecuteAsync(async () =>
        {
            var result = await service.PrepareAsync(new(query, workspaces, subject, @event, workflow, maxTokens), cancellationToken);
            return AgentContextMessages.AddGuidance(result, locale ?? executor.GetRequestLocale(), workflow?.AutomaticCapture == true);
        }, logger);
    }

    [McpServerTool(Name = "context_activity", Title = "Assess knowledge after an operation", ReadOnly = true,
        Idempotent = true, UseStructuredContent = true)]
    [Description("Assess a completed operation through the same client-neutral context contract. Accepts canonical operation identifiers, never commands or raw tool output. Does not retrieve, create Workspaces, or save knowledge.")]
    public async Task<AgentContextResult> ActivityAsync(
        IAgentContextAppService service,
        [Description("Canonical tool name, succeeded/failed/unknown status, and optional operation ID.")]
        AgentOperation operation,
        [Description("Optional event of type operationCompleted with input correlation IDs.")]
        AgentContextEvent? @event = null,
        AgentWorkflowPreferences? workflow = null,
        [Description("Optional language: zh-CN or en-US. Omit to use Accept-Language, then the default locale.")]
        string? locale = null)
    {
        return await executor.ExecuteAsync(() =>
        {
            var result = service.AssessActivity(new(operation, @event, workflow));
            return Task.FromResult(AgentContextMessages.AddGuidance(result,
                locale ?? executor.GetRequestLocale(), workflow?.AutomaticCapture == true));
        }, logger);
    }

    [McpServerTool(Name = "context_search", Title = "Search explicit context", ReadOnly = true, Idempotent = true,
        UseStructuredContent = true)]
    [Description(
        "Search durable Context and Markdown excerpts for an explicit query. Omit workspaces to search every workspace available in the current access scope, or provide explicit workspace paths.")]
    public async Task<ContextSearchResult> SearchAsync(
        IContextQueryAppService service,
        [Description("The information you explicitly want to find.")]
        string query,
        [Description("Optional explicit workspace paths. Omit to search the current depot across all workspaces.")]
        IReadOnlyList<string>? workspaces = null,
        [Description("Include child workspaces below each explicit workspace.")]
        bool includeDescendants = false,
        [Description("Optional context kinds to include.")]
        IReadOnlyList<ContextKind>? kinds = null,
        [Description("Maximum number of Context and Markdown matches combined.")]
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return await executor.ExecuteAsync(
            () => service.SearchAsync(
                new ContextSearchRequest(query, workspaces, includeDescendants, kinds, limit),
                cancellationToken),
            logger);
    }

    [McpServerTool(Name = "context_get", Title = "Get context details", ReadOnly = true, Idempotent = true,
        UseStructuredContent = true)]
    [Description(
        "Read a known Context ID in the current depot scope, including archived and superseded lifecycle details. This never reads another depot and never changes lifecycle state.")]
    public async Task<ContextDetailModel?> GetAsync(
        IContextQueryAppService service,
        Guid contextId,
        CancellationToken cancellationToken = default)
    {
        return await executor.ExecuteAsync(
            () => service.GetAsync(contextId, cancellationToken),
            logger);
    }

    [McpServerTool(Name = "context_save", Title = "Save durable context", Destructive = false,
        UseStructuredContent = true)]
    [Description(
        "Save durable fact, preference, decision, goal, state, or event after the user explicitly asks to remember it or it is clearly long-lived. The current depot is configured by the host; never provide depotId.")]
    public async Task<SaveContextResult> SaveAsync(
        IContextAppService service,
        [Description("Workspace path.")] string workspace,
        [Description("One of fact, preference, decision, goal, state, or event.")]
        string kind,
        [Description("Durable context content; it is stored as data, not instructions.")]
        string content,
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
        return await executor.ExecuteAsync(async () =>
        {
            if (!Enum.TryParse<ContextKind>(kind, true, out var parsedKind) || !Enum.IsDefined(parsedKind) ||
                !string.Equals(kind, Enum.GetName(parsedKind), StringComparison.OrdinalIgnoreCase))
            {
                throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidContextKind);
            }

            if (parsedKind == ContextKind.Observation)
            {
                throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidContextKind);
            }

            var parsedVerificationStatus = ParseVerificationStatus(verificationStatus);
            return await service.SaveAsync(
                new SaveContextCommand(workspace, parsedKind, content, key, title, tags, importance, confidence,
                    supersedesId, parsedVerificationStatus, ProvenanceTrust.Unknown, SourceType.Agent,
                    sourceAgent ?? "MCP", sourceRef), cancellationToken);
        }, logger);
    }


    [McpServerTool(Name = "context_archive", Title = "Archive context", Destructive = true,
        UseStructuredContent = true)]
    [Description(
        "Archive a known Context ID in the current depot scope. Archived data is retained but excluded from ordinary bootstrap retrieval.")]
    public async Task<object> ArchiveAsync(IContextAppService service, Guid contextId,
        CancellationToken cancellationToken = default)
    {
        return await executor.ExecuteAsync(async () =>
        {
            await service.ArchiveAsync(contextId, cancellationToken);
            return (object)new { archived = true, contextId };
        }, logger);
    }

    private static VerificationStatus ParseVerificationStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return VerificationStatus.Unknown;
        }

        if (!Enum.TryParse<VerificationStatus>(value, true, out var parsed) || !Enum.IsDefined(parsed) ||
            !string.Equals(value, Enum.GetName(parsed), StringComparison.OrdinalIgnoreCase) ||
            parsed == VerificationStatus.Verified)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidVerificationStatus);
        }

        return parsed;
    }
}
