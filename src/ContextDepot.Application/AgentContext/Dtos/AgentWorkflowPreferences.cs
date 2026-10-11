namespace ContextDepot.Application.AgentContext.Dtos;

public sealed record AgentWorkflowPreferences(bool AutomaticCapture = false, bool PrepareWorkspace = false);
