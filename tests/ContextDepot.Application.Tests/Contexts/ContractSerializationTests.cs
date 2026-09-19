using System.Text.Json;
using ContextDepot.Application.Contexts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Tests.Contexts;

public sealed class ContractSerializationTests
{
    [Fact]
    public void Public_enums_are_serialized_as_lower_camel_case_strings()
    {
        var context = new ContextModel(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            ContextKind.Preference,
            "editor.theme",
            "Theme",
            "dark",
            [],
            50,
            null,
            ContextStatus.Active,
            VerificationStatus.Explicit,
            ProvenanceTrust.Asserted,
            SourceType.Agent,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(context, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Contains("\"kind\":\"preference\"", json, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"active\"", json, StringComparison.Ordinal);
        Assert.Contains("\"verificationStatus\":\"explicit\"", json, StringComparison.Ordinal);
        Assert.Contains("\"provenanceTrust\":\"asserted\"", json, StringComparison.Ordinal);
    }
}
