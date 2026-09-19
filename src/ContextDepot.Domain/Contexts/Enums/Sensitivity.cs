using System.Text.Json.Serialization;
using ContextDepot.Domain.Shared;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<Sensitivity>))]
public enum Sensitivity
{
    Normal,
    Sensitive
}
