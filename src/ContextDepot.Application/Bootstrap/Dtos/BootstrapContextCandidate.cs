using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Bootstrap.Dtos;

public sealed record BootstrapContextCandidate(
    Guid Id,
    Guid OwnerId,
    Guid WorkspaceId,
    ContextKind Kind,
    string? Key,
    string? Title,
    string Content,
    string TagsJson,
    short Importance,
    decimal? Confidence,
    ContextStatus Status,
    VerificationStatus VerificationStatus,
    ProvenanceTrust ProvenanceTrust,
    SourceType SourceType,
    string? SourceAgent,
    string? SourceRef,
    Guid? SupersedesId,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string MetadataJson);
