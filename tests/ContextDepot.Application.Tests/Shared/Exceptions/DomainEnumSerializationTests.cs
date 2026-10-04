using System.Text.Json;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Documents.Enums;
using ContextDepot.Domain.Exceptions;

namespace ContextDepot.Application.Tests.Shared.Exceptions;

public sealed class DomainEnumSerializationTests
{
    [Fact]
    public void Invalid_enum_value_reports_stable_meaning_without_selecting_a_language()
    {
        var exception = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ContextKind>("\"not-a-kind\""));

        Assert.Equal(DomainErrorCodes.InvalidEnumValue, exception.Message);
    }

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("\"999\"")]
    [InlineData("\"Fact, Preference\"")]
    [InlineData("\"Preference, Preference\"")]
    [InlineData("\" preference \"")]
    [InlineData("1")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Numeric_and_combined_enum_names_are_rejected(string json)
    {
        var exception = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ContextKind>(json));

        Assert.Equal(DomainErrorCodes.InvalidEnumValue, exception.Message);
    }

    [Fact]
    public void Defined_enum_names_keep_the_lower_camel_case_contract()
    {
        Assert.Equal("\"preference\"", JsonSerializer.Serialize(ContextKind.Preference));
        Assert.Equal(ContextKind.Preference, JsonSerializer.Deserialize<ContextKind>("\"PREFERENCE\""));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((ContextKind)999));
    }

    [Fact]
    public void All_public_domain_enum_values_keep_their_wire_names_and_round_trip()
    {
        AssertContract<ContextKind>("[\"fact\",\"preference\",\"decision\",\"goal\",\"state\",\"event\",\"observation\"]");
        AssertContract<ContextStatus>("[\"active\",\"superseded\",\"archived\"]");
        AssertContract<ProvenanceTrust>("[\"unknown\",\"asserted\",\"attested\"]");
        AssertContract<Sensitivity>("[\"normal\",\"sensitive\"]");
        AssertContract<SourceType>("[\"agent\",\"user\",\"import\",\"system\"]");
        AssertContract<VerificationStatus>("[\"unknown\",\"explicit\",\"inferred\",\"verified\"]");
        AssertContract<DocumentIndexStatus>("[\"pending\",\"indexed\",\"failed\"]");
        AssertContract<DocumentStatus>("[\"active\",\"archived\"]");
    }

    private static void AssertContract<TEnum>(string expectedJson) where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        Assert.Equal(expectedJson, JsonSerializer.Serialize(values));
        Assert.Equal(values, JsonSerializer.Deserialize<TEnum[]>(expectedJson));
    }
}
