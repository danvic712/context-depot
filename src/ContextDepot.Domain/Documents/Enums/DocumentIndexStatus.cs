using System.Text.Json.Serialization;
using ContextDepot.Domain.Shared;

namespace ContextDepot.Domain.Documents.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<DocumentIndexStatus>))]
public enum DocumentIndexStatus
{
    Pending,
    Indexed,
    Failed
}
