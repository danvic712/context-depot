using ContextDepot.Application.Abstractions;
using ContextDepot.Application.Safety;
using ContextDepot.Domain.Entities;

namespace ContextDepot.Application.Tests;

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

        Assert.Equal("ProvenanceNotAllowed", exception.ErrorCode);
    }

    [Fact]
    public void Provenance_trust_is_computed_by_the_server()
    {
        var result = new ProvenancePolicy().Evaluate(new ProvenanceInput(SourceType.Agent));

        Assert.Equal(VerificationStatus.SelfReported, result.VerificationStatus);
        Assert.Equal(ProvenanceTrust.AgentReported, result.Trust);
    }
}
