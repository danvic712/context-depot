namespace ContextDepot.Application.AgentContext.Dtos;

public sealed record AgentContextResult(int SchemaVersion, AgentContextEvent Event,
    AgentWorkspaceResolution Workspace, AgentRetrievalResult Retrieval,
    IReadOnlyList<AgentContextAction> NextActions, AgentContextReceipt Receipt, string? Guidance = null);
