using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<SourceType>))]
public enum SourceType
{
    Agent,
    User,
    Import,
    System
}
