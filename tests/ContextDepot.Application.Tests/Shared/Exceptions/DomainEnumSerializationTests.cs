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
}
