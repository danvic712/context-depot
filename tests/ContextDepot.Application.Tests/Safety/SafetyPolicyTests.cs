using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Safety;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Shared.Safety.Dtos;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Tests.Safety;

public sealed class SafetyPolicyTests
{
    [Theory]
    [InlineData("token: sk-123456789012345678901234")]
    [InlineData("-----BEGIN PRIVATE KEY-----")]
    [InlineData("api_key = abcdefghijklmnop")]
    public void High_confidence_secrets_are_rejected(string content)
    {
        var result = new HighConfidenceSecretDetector().Detect(content);

        Assert.True(result.IsSecret);
        Assert.Equal("SecretContentRejected", result.ErrorCode);
    }

    [Theory]
    [InlineData("<api-key>")]
    [InlineData("${API_KEY}")]
    [InlineData("Use YOUR_API_KEY here")]
    public void Placeholders_are_not_rejected(string content)
    {
        Assert.False(new HighConfidenceSecretDetector().Detect(content).IsSecret);
    }

    [Fact]
    public void Agent_cannot_self_attest()
    {
        var policy = new ProvenancePolicy();

        var exception = Assert.Throws<ContextDepotApplicationException>(() => policy.Evaluate(new ProvenanceInput(
            SourceType.Agent,
            VerificationStatus.Verified,
            ProvenanceTrust.Attested)));

        Assert.Equal("InvalidVerificationStatus", exception.ErrorCode);
    }

    [Fact]
    public void Provenance_trust_is_computed_by_the_server()
    {
        var result = new ProvenancePolicy().Evaluate(new ProvenanceInput(SourceType.Agent));

        Assert.Equal(VerificationStatus.Unknown, result.VerificationStatus);
        Assert.Equal(ProvenanceTrust.Asserted, result.Trust);
    }

    [Fact]
    public void Untrusted_system_source_is_rejected()
    {
        var exception = Assert.Throws<ContextDepotApplicationException>(() => new ProvenancePolicy().Evaluate(new ProvenanceInput(SourceType.System)));

        Assert.Equal(ApplicationErrorCodes.InvalidVerificationStatus, exception.ErrorCode);
    }

    [Fact]
    public void Trusted_server_can_attest_verified_content()
    {
        var result = new ProvenancePolicy().Evaluate(new ProvenanceInput(
            SourceType.System,
            VerificationStatus.Verified,
            ProvenanceTrust.Attested,
            IsTrustedServer: true));

        Assert.Equal(VerificationStatus.Verified, result.VerificationStatus);
        Assert.Equal(ProvenanceTrust.Attested, result.Trust);
    }
}
