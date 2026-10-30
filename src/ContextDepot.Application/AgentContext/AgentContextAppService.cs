using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ContextDepot.Application.AgentContext.Contracts;
using ContextDepot.Application.AgentContext.Dtos;
using ContextDepot.Application.AgentContext.Enums;
using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Workspaces.Enums;
using ContextDepot.Application.Workspaces;

namespace ContextDepot.Application.AgentContext;

public sealed class AgentContextAppService(IContextBootstrapAppService bootstrap, IWorkspaceAppService workspaces,
    IWorkspaceAccessContext access, ISourceSafetyService safety, IIdGenerator ids, TimeProvider clock) : IAgentContextAppService
{
    private static readonly HashSet<string> DepotOperations = new(StringComparer.Ordinal)
    {
        "context_bootstrap", "context_activity", "context_search",
        "session_start", "session_capture", "session_recall", "session_end", "session_status", "context_get", "context_save", "context_archive", "document_upsert", "document_get", "document_archive",
        "workspace_list", "workspace_upsert", "depot_get"
    };

    public async Task<AgentContextResult> PrepareAsync(PrepareAgentContextRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contextEvent = request.Event ?? new();
        ValidateEvent(contextEvent);
        if (contextEvent.Type == AgentContextEventType.OperationCompleted || request.Query is null) Invalid();
        if (request.MaxTokens is < 1 or > 8000)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.ContextBudgetInvalid);
        var subject = request.Subject is null ? null : WorkspaceSubjectNormalizer.Normalize(request.Subject, safety);
        var input = request.Query.Trim().TrimEnd('.', '!', '。', '！');
        if (contextEvent.Type == AgentContextEventType.TaskInput && (input.Length == 0 || AgentContextMessages.IsAcknowledgement(input)))
            return Result(contextEvent, new(subject, WorkspaceSubjectStatus.NotEvaluated, null, [], null),
                new(AgentRetrievalStatus.Skipped, input.Length == 0 ? "emptyInput" : "acknowledgement", null), []);

        var (resolution, needsBinding) = await ResolveWorkspaceAsync(subject, request.Workspaces, cancellationToken);
        var query = contextEvent.Type == AgentContextEventType.TaskInput ? NormalizeAutomaticQuery(request.Query) : request.Query;
        var retrievalPaths = request.Workspaces is { Count: > 0 } ? request.Workspaces :
            resolution.Target is not null ? new[] { resolution.Target.Path } : null;
        var result = await bootstrap.BootstrapAsync(new(query, retrievalPaths, request.MaxTokens), cancellationToken);
        // Auto Scope is retrieval evidence; it never establishes a save destination.
        if (resolution.Target is null && resolution.Status != WorkspaceSubjectStatus.Unavailable && resolution.Candidates.Count == 0)
        {
            var candidates = new List<WorkspaceModel>();
            foreach (var path in result.ScopeResolution.Workspaces.Distinct(StringComparer.Ordinal))
            {
                var candidate = await workspaces.ResolveAsync(path, cancellationToken);
                if (candidate is not null) candidates.Add(candidate);
            }
            resolution = resolution with
            {
                Candidates = candidates,
                Status = candidates.Count > 1 ? WorkspaceSubjectStatus.Ambiguous : WorkspaceSubjectStatus.NeedsResolution
            };
        }
        var actions = new List<AgentContextAction>();
        if (request.Workflow?.PrepareWorkspace == true && needsBinding)
            actions.Add(new(AgentContextActionType.EnsureWorkspace, "explicitTargetNeedsBinding", "workspace_upsert"));
        else if (request.Workflow?.PrepareWorkspace == true && resolution.Status is WorkspaceSubjectStatus.NeedsResolution or WorkspaceSubjectStatus.Ambiguous)
        {
            if (subject is not null && resolution.Candidates.Count == 0 && resolution.CanCreateRootWorkspaces == true)
                actions.Add(new(AgentContextActionType.EnsureWorkspace, "noAccessibleBinding", "workspace_upsert"));
            else actions.Add(new(AgentContextActionType.ResolveWorkspace, "explicitDestinationRequired", "workspace_list"));
        }
        return Result(contextEvent, resolution, new(AgentRetrievalStatus.Completed, null, result), actions);
    }

    public AgentContextResult AssessActivity(AgentActivityRequest request)
    {
        var contextEvent = request.Event ?? new(AgentContextEventType.OperationCompleted);
        ValidateEvent(contextEvent);
        if (contextEvent.Type != AgentContextEventType.OperationCompleted || request.Operation is null ||
            string.IsNullOrWhiteSpace(request.Operation.ToolName) || request.Operation.ToolName.Length > 200 ||
            !Enum.IsDefined(request.Operation.Status) || request.Operation.OperationId?.Length > 200) Invalid();
        var self = DepotOperations.Contains(request.Operation.ToolName.Trim());
        var capture = !self && request.Workflow?.AutomaticCapture == true;
        return Result(contextEvent, new(null, WorkspaceSubjectStatus.NotEvaluated, null, [], null),
            new(capture ? AgentRetrievalStatus.NotRequested : AgentRetrievalStatus.Skipped,
                self ? "depotOperation" : !capture ? "captureDisabled" : null, null),
            capture ? [new(AgentContextActionType.AssessCapture, "operationEvidenceAvailable")] : []);
    }

    private async Task<(AgentWorkspaceResolution Resolution, bool NeedsBinding)> ResolveWorkspaceAsync(WorkspaceSubject? subject,
        IReadOnlyList<string>? paths, CancellationToken cancellationToken)
    {
        WorkspaceSubjectResolution? binding = subject is null ? null : await workspaces.ResolveSubjectAsync(subject, cancellationToken);
        if (binding?.Status is WorkspaceSubjectStatus.Resolved or WorkspaceSubjectStatus.Unavailable)
            return (new(subject, binding.Status, binding.Target, [], binding.CanCreateRootWorkspaces), false);
        var candidates = new List<WorkspaceModel>();
        if (paths is not null)
            foreach (var path in paths.Distinct(StringComparer.Ordinal))
                candidates.Add(await workspaces.ResolveAsync(path, cancellationToken) ??
                    throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound));
        var target = candidates.Count == 1 ? candidates[0] : null;
        return (new(subject, target is not null ? WorkspaceSubjectStatus.Resolved : candidates.Count > 1 ?
            WorkspaceSubjectStatus.Ambiguous : WorkspaceSubjectStatus.NeedsResolution, target, candidates,
            binding?.CanCreateRootWorkspaces ?? (access.HasUnrestrictedAccess || access.CanCreateRootWorkspaces)), subject is not null && target is not null);
    }

    private AgentContextResult Result(AgentContextEvent contextEvent, AgentWorkspaceResolution workspace,
        AgentRetrievalResult retrieval, IReadOnlyList<AgentContextAction> actions) =>
        new(1, contextEvent, workspace, retrieval, actions,
            new(ids.NewId(), clock.GetUtcNow(), access.AccessVersion.ToString(CultureInfo.InvariantCulture)));

    private static void ValidateEvent(AgentContextEvent contextEvent)
    {
        if (!Enum.IsDefined(contextEvent.Type) || contextEvent.EventId?.Length > 200 || contextEvent.ConversationId?.Length > 200) Invalid();
    }

    private static string NormalizeAutomaticQuery(string query)
    {
        if (query.Length <= 6000) return query;
        var head = 2999;
        var tail = query.Length - 3000;
        if (char.IsHighSurrogate(query[head - 1])) head--;
        if (char.IsLowSurrogate(query[tail])) tail++;
        return string.Concat(query.AsSpan(0, head), "\n", query.AsSpan(tail));
    }

    [DoesNotReturn]
    private static void Invalid() => throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidRequest);
}
