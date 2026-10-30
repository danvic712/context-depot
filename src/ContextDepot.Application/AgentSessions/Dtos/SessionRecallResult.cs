using ContextDepot.Application.Bootstrap.Dtos;

namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record SessionRecallResult(SessionModel Session, BootstrapResult Context);
