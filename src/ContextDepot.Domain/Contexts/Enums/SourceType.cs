using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<SourceType>))]
public enum SourceType
{
    [JsonStringEnumMemberName("agent")]
    Agent,
    [JsonStringEnumMemberName("user")]
    User,
    [JsonStringEnumMemberName("import")]
    Import,
    [JsonStringEnumMemberName("system")]
    System
}
