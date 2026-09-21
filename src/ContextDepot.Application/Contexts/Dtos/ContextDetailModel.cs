using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Contexts.Dtos;

public sealed record ContextDetailModel(
    Guid Id,
    Guid DepotId,
    Guid WorkspaceId,
    string Workspace,
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
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    DateTimeOffset? ExpiresAt,
    Sensitivity Sensitivity,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
