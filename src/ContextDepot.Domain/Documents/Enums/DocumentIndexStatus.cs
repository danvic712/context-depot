using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Documents.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<DocumentIndexStatus>))]
public enum DocumentIndexStatus
{
    Pending,
    Indexed,
    Failed
}
