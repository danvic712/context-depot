using ContextDepot.Domain.Entities;

namespace ContextDepot.Application.Contexts;

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
    VerificationStatus VerificationStatus = VerificationStatus.Unverified,
    ProvenanceTrust RequestedProvenanceTrust = ProvenanceTrust.Unknown,
    SourceType SourceType = SourceType.Agent,
    string? SourceAgent = null,
    string? SourceRef = null,
    DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidUntil = null,
    DateTimeOffset? ExpiresAt = null,
    string MetadataJson = "{}");

public enum SaveContextOutcome
{
    Created,
    UpdatedCurrentTruth,
    ReusedExisting,
    SupersededExisting
}

public sealed record ContextModel(
    Guid Id,
    Guid OwnerId,
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

public sealed record SaveContextResult(ContextModel Context, SaveContextOutcome Outcome, Guid? PreviousContextId = null);

public interface IContextAppService
{
    Task<SaveContextResult> SaveAsync(SaveContextCommand command, CancellationToken cancellationToken);

    Task ArchiveAsync(Guid contextId, CancellationToken cancellationToken);
}
