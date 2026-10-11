using System.Text.Json.Serialization;
using ContextDepot.Domain;

namespace ContextDepot.Application.AgentContext.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<AgentRetrievalStatus>))]
public enum AgentRetrievalStatus
{
    [JsonStringEnumMemberName("completed")]
    Completed,
    [JsonStringEnumMemberName("skipped")]
    Skipped,
    [JsonStringEnumMemberName("notRequested")]
    NotRequested
}
