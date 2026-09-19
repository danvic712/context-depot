using System.Text.Json.Serialization;
using ContextDepot.Domain.Shared;

namespace ContextDepot.Domain.Documents.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<DocumentStatus>))]
public enum DocumentStatus
{
    Active,
    Archived
}
