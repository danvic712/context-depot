using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<ContextStatus>))]
public enum ContextStatus
{
    [JsonStringEnumMemberName("active")]
    Active,
    [JsonStringEnumMemberName("superseded")]
    Superseded,
    [JsonStringEnumMemberName("archived")]
    Archived
}
