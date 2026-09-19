using System.Text.Json.Serialization;
using ContextDepot.Domain.Shared;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<SourceType>))]
public enum SourceType
{
    Agent,
    User,
    Import,
    System
}
