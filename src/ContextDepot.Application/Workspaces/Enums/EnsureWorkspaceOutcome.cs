using ContextDepot.Domain;
using System.Text.Json.Serialization;

namespace ContextDepot.Application.Workspaces.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<EnsureWorkspaceOutcome>))]
public enum EnsureWorkspaceOutcome
{
    [JsonStringEnumMemberName("created")]
    Created,
    [JsonStringEnumMemberName("reusedExisting")]
    ReusedExisting,
    [JsonStringEnumMemberName("boundExisting")]
    BoundExisting
}
