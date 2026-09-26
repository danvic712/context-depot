using System.Text.Json;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Tests.Shared.Exceptions;

public sealed class DomainEnumSerializationTests
{
    [Fact]
    public void Invalid_enum_value_reports_stable_meaning_without_selecting_a_language()
    {
        var exception = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ContextKind>("\"not-a-kind\""));

        Assert.Equal("Invalid ContextKind value.", exception.Message);
    }

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("\"999\"")]
    [InlineData("\"Fact, Preference\"")]
    public void Numeric_and_combined_enum_names_are_rejected(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ContextKind>(json));
    }

    [Fact]
    public void Defined_enum_names_keep_the_lower_camel_case_contract()
    {
        Assert.Equal("\"preference\"", JsonSerializer.Serialize(ContextKind.Preference));
        Assert.Equal(ContextKind.Preference, JsonSerializer.Deserialize<ContextKind>("\"PREFERENCE\""));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((ContextKind)999));
    }
}
