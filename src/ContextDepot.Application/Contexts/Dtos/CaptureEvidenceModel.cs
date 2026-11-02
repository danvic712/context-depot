using ContextDepot.Application.AgentSessions.Dtos;
namespace ContextDepot.Application.Contexts.Dtos;

public sealed record CaptureEvidenceModel(string State, Guid SourceUserReceiptId, Guid SourceAssistantReceiptId,
    string ExtractorVersion, IReadOnlyList<EvidenceFragment> Fragments);
