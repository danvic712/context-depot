using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Documents.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<DocumentStatus>))]
public enum DocumentStatus
{
    Active,
    Archived
}
