using System.Text.Json.Serialization;
using ContextDepot.Domain;

namespace ContextDepot.Application.Workspaces.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<WorkspaceSubjectStatus>))]
public enum WorkspaceSubjectStatus
{
    [JsonStringEnumMemberName("resolved")]
    Resolved,
    [JsonStringEnumMemberName("needsResolution")]
    NeedsResolution,
    [JsonStringEnumMemberName("ambiguous")]
    Ambiguous,
    [JsonStringEnumMemberName("unavailable")]
    Unavailable,
    [JsonStringEnumMemberName("notEvaluated")]
    NotEvaluated
}
