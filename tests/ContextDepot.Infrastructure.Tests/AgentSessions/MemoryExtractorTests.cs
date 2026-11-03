using System.Text.Json;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Infrastructure.AgentSessions;
using Microsoft.AspNetCore.DataProtection;

namespace ContextDepot.Infrastructure.Tests.AgentSessions;

public sealed class MemoryExtractorTests
{
    private const string Valid = """{"candidates":[{"kind":"fact","key":"project.database","summary":"用户使用 PostgreSQL 数据库。","evidence":[{"quote":"用户的项目使用 PostgreSQL 数据库。"}]}]}""";

    [Fact]
    public void Provider_schema_contains_summary_and_quotes_not_offsets_or_confidence()
    {
        var candidate = Assert.Single(AgentFrameworkMemoryExtractor.ParseResponse(Valid));
        Assert.Equal("用户使用 PostgreSQL 数据库。", candidate.Summary);
        Assert.Equal("用户的项目使用 PostgreSQL 数据库。", Assert.Single(candidate.Quotes!));
        Assert.Equal("", candidate.Content);
    }

    [Theory]
    [InlineData("{\"candidates\":[],\"candidates\":[]}")]
    [InlineData("{\"candidates\":[],\"workspaceId\":\"override\"}")]
    [InlineData("{\"candidates\":[null]}")]
    [InlineData("{\"candidates\":[{\"kind\":\"fact\",\"key\":\"a\",\"summary\":\"test\",\"summary\":\"other\",\"evidence\":[]}]}")]
    [InlineData("{\"candidates\":[{\"kind\":\"fact\",\"key\":\"a\",\"summary\":\"test\",\"evidence\":[{\"quote\":\"test\",\"start\":0}]}]}")]
    [InlineData("{\"candidates\":[{\"kind\":\"fact\",\"key\":\"a\",\"summary\":\"test\",\"evidence\":[{\"quote\":\"test\",\"quote\":\"other\"}]}]}")]
    public void Extra_or_duplicate_properties_are_rejected(string text) =>
        Assert.Throws<JsonException>(() => AgentFrameworkMemoryExtractor.ParseResponse(text));

    [Fact]
    public void Legacy_provider_schema_is_not_accepted() => Assert.Throws<JsonException>(() =>
        AgentFrameworkMemoryExtractor.ParseResponse("""{"candidates":[{"kind":"fact","key":"a","content":"legacy","evidence":"legacy","confidence":1}]}"""));

    [Fact]
    public void Plan_round_trips_canonically_and_detects_tampering()
    {
        var plan = new ValidatedExtractionPlan(ValidatedExtractionPlan.CurrentVersion,
            [new(0, "fact", "project.database", "用户使用 PostgreSQL 数据库。", [new(0, 4, "原始证据")])]);
        var text = CapturePlanCodec.Serialize(plan);
        var hash = CapturePlanCodec.Hash(text);
        Assert.Equal(text, CapturePlanCodec.Serialize(CapturePlanCodec.Read(text, hash)));
        Assert.Throws<InvalidOperationException>(() => CapturePlanCodec.Read(text + " ", hash));
        Assert.Throws<InvalidOperationException>(() => CapturePlanCodec.Serialize(plan with { Version = "unknown" }));
    }

    [Fact]
    public void Evidence_protection_is_isolated_from_temporary_capture_content()
    {
        var provider = new EphemeralDataProtectionProvider();
        var evidence = new CaptureEvidenceProtector(provider);
        var content = new SessionContentProtector(provider);
        var ciphertext = evidence.Protect("original evidence");
        Assert.Equal("original evidence", evidence.Unprotect(ciphertext));
        Assert.Throws<System.Security.Cryptography.CryptographicException>(() => content.Unprotect(ciphertext));
    }
}
