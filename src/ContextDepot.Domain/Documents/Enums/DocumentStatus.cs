using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Documents.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<DocumentStatus>))]
public enum DocumentStatus
{
    [JsonStringEnumMemberName("active")]
    Active,
    [JsonStringEnumMemberName("archived")]
    Archived
}
