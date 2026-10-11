using ContextDepot.Application.AgentContext.Enums;

namespace ContextDepot.Application.AgentContext.Dtos;

public sealed record AgentOperation(string ToolName, AgentOperationStatus Status = AgentOperationStatus.Unknown,
    string? OperationId = null);
