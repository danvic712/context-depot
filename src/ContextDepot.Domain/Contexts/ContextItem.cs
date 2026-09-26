using ContextDepot.Domain.Workspaces;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Domain.Contexts;

public sealed class ContextItem
{
    private ContextItem()
    {
    }

    public ContextItem(Guid id, Guid depotId, Guid workspaceId, ContextKind kind, string? key, string? title, string content, DateTimeOffset now)
    {
        Id = id;
        DepotId = depotId;
        WorkspaceId = workspaceId;
        Kind = kind;
        Key = key;
        Title = title;
        Content = content;
        Status = ContextStatus.Active;
        VerificationStatus = VerificationStatus.Unknown;
        ProvenanceTrust = ProvenanceTrust.Unknown;
        SourceType = SourceType.Agent;
        Sensitivity = Sensitivity.Normal;
        Importance = 50;
        TagsJson = "[]";
        MetadataJson = "{}";
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid DepotId { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public ContextKind Kind { get; private set; }

    public string? Key { get; private set; }

    public string? Title { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public string TagsJson { get; private set; } = "[]";

    public short Importance { get; private set; }

    public ContextStatus Status { get; private set; }

    public VerificationStatus VerificationStatus { get; private set; }

    public ProvenanceTrust ProvenanceTrust { get; private set; }

    public decimal? Confidence { get; private set; }

    public SourceType SourceType { get; private set; }

    public string? SourceAgent { get; private set; }

    public string? SourceRef { get; private set; }

    public Guid? SupersedesId { get; private set; }

    public DateTimeOffset? ValidFrom { get; private set; }

    public DateTimeOffset? ValidUntil { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public Sensitivity Sensitivity { get; private set; }

    public string MetadataJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Workspace? Workspace { get; private set; }

    public ContextItem? Supersedes { get; private set; }

    public void ConfigureProvenance(VerificationStatus verificationStatus, ProvenanceTrust provenanceTrust, SourceType sourceType, string? sourceAgent, string? sourceRef)
    {
        VerificationStatus = verificationStatus;
        ProvenanceTrust = provenanceTrust;
        SourceType = sourceType;
        SourceAgent = sourceAgent;
        SourceRef = sourceRef;
    }

    public void MarkSuperseded(DateTimeOffset now)
    {
        Status = ContextStatus.Superseded;
        UpdatedAt = now;
    }

    public void MarkArchived(DateTimeOffset now)
    {
        Status = ContextStatus.Archived;
        UpdatedAt = now;
    }

    public void SetSupersedes(Guid? supersedesId)
    {
        SupersedesId = supersedesId;
    }

    public void SetTags(string tagsJson) => TagsJson = tagsJson;

    public void SetMetadata(string metadataJson) => MetadataJson = metadataJson;

    public void SetValidity(DateTimeOffset? validFrom, DateTimeOffset? validUntil, DateTimeOffset? expiresAt)
    {
        if (validFrom is not null && validUntil is not null && validFrom >= validUntil)
        {
            throw new ArgumentException("The valid-from time must be earlier than the valid-until time.", nameof(validUntil));
        }

        ValidFrom = validFrom;
        ValidUntil = validUntil;
        ExpiresAt = expiresAt;
    }

    public void SetQuality(short importance, decimal? confidence)
    {
        if (importance is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(importance));
        }

        if (confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        Importance = importance;
        Confidence = confidence;
    }
}
