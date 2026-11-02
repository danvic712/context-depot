namespace ContextDepot.Domain.Contexts;

public sealed class ContextCaptureEvidence
{
    public Guid ContextId { get; set; }
    public Guid DepotId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid AccessKeyId { get; set; }
    public Guid UserEventId { get; set; }
    public Guid AssistantEventId { get; set; }
    public int CandidateIndex { get; set; }
    public string UserContentHash { get; set; } = string.Empty;
    public string EncryptedFragments { get; set; } = string.Empty;
    public string FragmentsHash { get; set; } = string.Empty;
    public string ExtractorVersion { get; set; } = string.Empty;
    public int ProtectionVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
}
