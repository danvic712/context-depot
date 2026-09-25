using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<ContextStatus>))]
public enum ContextStatus
{
    Active,
    Superseded,
    Archived
}
