namespace ContextDepot.Application.AgentContext.Dtos;

public sealed record AgentActivityRequest(AgentOperation Operation, AgentContextEvent? Event = null,
    AgentWorkflowPreferences? Workflow = null);
