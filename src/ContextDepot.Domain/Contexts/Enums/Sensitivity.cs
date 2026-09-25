using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<Sensitivity>))]
public enum Sensitivity
{
    Normal,
    Sensitive
}
