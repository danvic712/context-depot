namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record CaptureWorkLease(Guid DepotId, Guid KeyId, string OwnerKind, Guid OwnerId, Guid LeaseToken);
