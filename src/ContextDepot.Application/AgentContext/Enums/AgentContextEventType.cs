using System.Text.Json.Serialization;
using ContextDepot.Domain;

namespace ContextDepot.Application.AgentContext.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<AgentContextEventType>))]
public enum AgentContextEventType
{
    [JsonStringEnumMemberName("contextRequest")]
    ContextRequest,
    [JsonStringEnumMemberName("taskInput")]
    TaskInput,
    [JsonStringEnumMemberName("operationCompleted")]
    OperationCompleted
}
