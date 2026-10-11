using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Workspaces;

[JsonConverter(typeof(LowerCaseEnumConverter<WorkspaceSubjectType>))]
public enum WorkspaceSubjectType
{
    [JsonStringEnumMemberName("project")]
    Project,
    [JsonStringEnumMemberName("topic")]
    Topic,
    [JsonStringEnumMemberName("personal")]
    Personal
}
