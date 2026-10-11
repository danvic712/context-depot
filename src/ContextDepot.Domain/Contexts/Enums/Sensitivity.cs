using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<Sensitivity>))]
public enum Sensitivity
{
    [JsonStringEnumMemberName("normal")]
    Normal,
    [JsonStringEnumMemberName("sensitive")]
    Sensitive
}
