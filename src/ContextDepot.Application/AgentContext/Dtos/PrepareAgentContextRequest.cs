using ContextDepot.Application.Workspaces.Dtos;

namespace ContextDepot.Application.AgentContext.Dtos;

public sealed record PrepareAgentContextRequest(string Query, IReadOnlyList<string>? Workspaces = null,
    WorkspaceSubject? Subject = null, AgentContextEvent? Event = null,
    AgentWorkflowPreferences? Workflow = null, int MaxTokens = 1200);
