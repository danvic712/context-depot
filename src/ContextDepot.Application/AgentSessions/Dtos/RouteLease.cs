namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record RouteLease(RouteLocator Locator, Guid LeaseToken, DateTimeOffset LeaseUntil,
    DateTimeOffset ExpiresAt, string UserContent, string UserContentHash,
    string? EncryptedRoutingVector = null, string? RoutingVectorHash = null,
    string? RoutingEmbeddingFingerprint = null);
