using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Shared.Safety.Dtos;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Shared.Safety;

public sealed class ProvenancePolicy : IProvenancePolicy
{
    public ProvenanceDecision Evaluate(ProvenanceInput input)
    {
        if (!input.IsTrustedServer &&
            (input.RequestedTrust != ProvenanceTrust.Unknown ||
             input.VerificationStatus == VerificationStatus.Verified ||
             input.SourceType == SourceType.System))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidVerificationStatus);
        }

        var trust = input.IsTrustedServer
            ? input.RequestedTrust == ProvenanceTrust.Unknown ? ProvenanceTrust.Attested : input.RequestedTrust
            : input.SourceType switch
            {
                SourceType.Agent or SourceType.User or SourceType.Import => ProvenanceTrust.Asserted,
                _ => ProvenanceTrust.Unknown
            };

        var verification = input.IsTrustedServer ? input.VerificationStatus : input.VerificationStatus;
        return new ProvenanceDecision(verification, trust, input.SourceType, input.SourceAgent, input.SourceRef);
    }
}
