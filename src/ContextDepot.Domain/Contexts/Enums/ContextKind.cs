using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<ContextKind>))]
public enum ContextKind
{
    [JsonStringEnumMemberName("fact")]
    Fact,
    [JsonStringEnumMemberName("preference")]
    Preference,
    [JsonStringEnumMemberName("decision")]
    Decision,
    [JsonStringEnumMemberName("goal")]
    Goal,
    [JsonStringEnumMemberName("state")]
    State,
    [JsonStringEnumMemberName("event")]
    Event,
    [JsonStringEnumMemberName("observation")]
    Observation
}
