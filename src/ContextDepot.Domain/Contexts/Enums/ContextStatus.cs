using System.Text.Json.Serialization;
using ContextDepot.Domain.Shared;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<ContextStatus>))]
public enum ContextStatus
{
    Active,
    Superseded,
    Archived
}
