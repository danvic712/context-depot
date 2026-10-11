using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Documents.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<DocumentIndexStatus>))]
public enum DocumentIndexStatus
{
    [JsonStringEnumMemberName("pending")]
    Pending,
    [JsonStringEnumMemberName("indexed")]
    Indexed,
    [JsonStringEnumMemberName("failed")]
    Failed
}
