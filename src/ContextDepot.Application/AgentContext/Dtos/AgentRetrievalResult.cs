using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.AgentContext.Enums;

namespace ContextDepot.Application.AgentContext.Dtos;

public sealed record AgentRetrievalResult(AgentRetrievalStatus Status, string? Reason,
    BootstrapResult? Result);
