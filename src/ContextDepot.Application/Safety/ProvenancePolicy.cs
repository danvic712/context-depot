using ContextDepot.Application.Abstractions;
using ContextDepot.Domain.Entities;

namespace ContextDepot.Application.Safety;

public sealed record ProvenanceInput(
    SourceType SourceType,
    VerificationStatus VerificationStatus = VerificationStatus.Unverified,
    ProvenanceTrust RequestedTrust = ProvenanceTrust.Unknown,
    string? SourceAgent = null,
    string? SourceRef = null,
    bool IsTrustedServer = false);

public sealed record ProvenanceDecision(
    VerificationStatus VerificationStatus,
    ProvenanceTrust Trust,
    SourceType SourceType,
    string? SourceAgent,
    string? SourceRef);

public interface IProvenancePolicy
{
    ProvenanceDecision Evaluate(ProvenanceInput input);
}

public sealed class ProvenancePolicy : IProvenancePolicy
{
    public ProvenanceDecision Evaluate(ProvenanceInput input)
    {
        if (!input.IsTrustedServer && (input.RequestedTrust == ProvenanceTrust.Attested || input.VerificationStatus == VerificationStatus.Verified))
        {
            throw new ContextDepotApplicationException("ProvenanceNotAllowed", "The requested provenance status is not allowed for this source.");
        }

        var trust = input.IsTrustedServer
            ? input.RequestedTrust == ProvenanceTrust.Unknown ? ProvenanceTrust.Verified : input.RequestedTrust
            : input.SourceType switch
            {
                SourceType.User => ProvenanceTrust.AgentReported,
                SourceType.Import => ProvenanceTrust.Imported,
                SourceType.System => ProvenanceTrust.Verified,
                _ => ProvenanceTrust.AgentReported
            };

        var verification = input.IsTrustedServer ? input.VerificationStatus : input.VerificationStatus == VerificationStatus.Unverified ? VerificationStatus.SelfReported : input.VerificationStatus;
        return new ProvenanceDecision(verification, trust, input.SourceType, input.SourceAgent, input.SourceRef);
    }
}
