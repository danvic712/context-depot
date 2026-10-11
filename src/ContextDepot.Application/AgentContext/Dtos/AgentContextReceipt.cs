namespace ContextDepot.Application.AgentContext.Dtos;

public sealed record AgentContextReceipt(Guid ResultId, DateTimeOffset GeneratedAt, string AccessVersion);
