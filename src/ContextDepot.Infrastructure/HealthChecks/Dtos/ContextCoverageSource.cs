using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Infrastructure.HealthChecks.Dtos;

public sealed record ContextCoverageSource(
    Guid Id,
    Guid WorkspaceId,
    ContextKind Kind,
    string? Key,
    string? Title,
    string TagsJson,
    string Content);
