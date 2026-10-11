using System.Text.Json.Serialization;
using ContextDepot.Application.AgentContext.Enums;

namespace ContextDepot.Application.AgentContext.Dtos;

public sealed record AgentContextEvent([property: JsonRequired] AgentContextEventType Type = AgentContextEventType.ContextRequest,
    string? EventId = null, string? ConversationId = null);
