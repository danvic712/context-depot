using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.IndexRepair.Dtos;

public sealed record ContextEmbeddingRepairCandidate(
    Guid ContextItemId,
    Guid OwnerId,
    Guid WorkspaceId,
    string WorkspacePath,
    ContextKind Kind,
    string? Key,
    string? Title,
    string TagsJson,
    string Content);
