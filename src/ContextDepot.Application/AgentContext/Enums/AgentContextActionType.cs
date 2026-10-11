using System.Text.Json.Serialization;
using ContextDepot.Domain;

namespace ContextDepot.Application.AgentContext.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<AgentContextActionType>))]
public enum AgentContextActionType
{
    [JsonStringEnumMemberName("ensureWorkspace")]
    EnsureWorkspace,
    [JsonStringEnumMemberName("resolveWorkspace")]
    ResolveWorkspace,
    [JsonStringEnumMemberName("assessCapture")]
    AssessCapture
}
