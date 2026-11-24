using System.Text.Json.Serialization;

namespace ContextDepot.Application.Workspaces.Dtos;

public sealed record WorkspaceModel(
    Guid Id,
    string Path,
    string Name,
    string? Description,
    string MetadataJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    [JsonIgnore]
    public string StoragePath { get; init; } = $"workspaces/{Id:N}";
}
