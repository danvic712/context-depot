using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Shared.Safety.Dtos;

public sealed record ProvenanceInput(
    SourceType SourceType,
    VerificationStatus VerificationStatus = VerificationStatus.Unknown,
    ProvenanceTrust RequestedTrust = ProvenanceTrust.Unknown,
    string? SourceAgent = null,
    string? SourceRef = null,
    bool IsTrustedServer = false);
