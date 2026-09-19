using System.Text.Json.Serialization;
using ContextDepot.Domain.Shared;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<ContextKind>))]
public enum ContextKind
{
    Fact,
    Preference,
    Decision,
    Goal,
    State,
    Event,
    Observation
}
