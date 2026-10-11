using System.Text.Json.Serialization;
using ContextDepot.Domain;

namespace ContextDepot.Application.Workspaces.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<WorkspaceUpsertMode>))]
public enum WorkspaceUpsertMode
{
    [JsonStringEnumMemberName("upsert")]
    Upsert,
    [JsonStringEnumMemberName("ensure")]
    Ensure
}
