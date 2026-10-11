using System.Text.Json.Serialization;
using ContextDepot.Domain;

namespace ContextDepot.Application.AgentContext.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<AgentOperationStatus>))]
public enum AgentOperationStatus
{
    [JsonStringEnumMemberName("succeeded")]
    Succeeded,
    [JsonStringEnumMemberName("failed")]
    Failed,
    [JsonStringEnumMemberName("unknown")]
    Unknown
}
