using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Infrastructure.Dtos;

public sealed record ContextCoverageSource(
    Guid Id,
    Guid DepotId,
    Guid WorkspaceId,
    ContextKind Kind,
    string? Key,
    string? Title,
    string TagsJson,
    string Content);
