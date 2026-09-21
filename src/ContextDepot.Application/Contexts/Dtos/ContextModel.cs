using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Contexts.Dtos;

public sealed record ContextModel(
    Guid Id,
    Guid DepotId,
    Guid WorkspaceId,
    ContextKind Kind,
    string? Key,
    string? Title,
    string Content,
    IReadOnlyList<string> Tags,
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
    DateTimeOffset UpdatedAt);
