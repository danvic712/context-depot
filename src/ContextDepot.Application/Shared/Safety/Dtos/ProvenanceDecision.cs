using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Shared.Safety.Dtos;

public sealed record ProvenanceDecision(
    VerificationStatus VerificationStatus,
    ProvenanceTrust Trust,
    SourceType SourceType,
    string? SourceAgent,
    string? SourceRef);
