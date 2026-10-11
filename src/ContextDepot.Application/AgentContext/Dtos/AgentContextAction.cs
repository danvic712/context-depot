using ContextDepot.Application.AgentContext.Enums;

namespace ContextDepot.Application.AgentContext.Dtos;

public sealed record AgentContextAction(AgentContextActionType Type, string ReasonCode, string? ToolName = null);
