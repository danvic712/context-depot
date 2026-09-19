using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Contexts.Dtos;

public sealed record SaveContextCommand(
    string Workspace,
    ContextKind Kind,
    string Content,
    string? Key = null,
    string? Title = null,
    IReadOnlyList<string>? Tags = null,
    short Importance = 50,
    decimal? Confidence = null,
    Guid? SupersedesId = null,
    VerificationStatus VerificationStatus = VerificationStatus.Unknown,
    ProvenanceTrust RequestedProvenanceTrust = ProvenanceTrust.Unknown,
    SourceType SourceType = SourceType.Agent,
    string? SourceAgent = null,
    string? SourceRef = null,
    DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidUntil = null,
    DateTimeOffset? ExpiresAt = null,
    string MetadataJson = "{}",
    bool IsTrustedServer = false);
