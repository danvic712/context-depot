namespace ContextDepot.Application.Settings.Dtos;

public sealed record AccessKeyDto(Guid Id, string Name, string Prefix, DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt, IReadOnlyList<Guid> WorkspaceIds,
    bool CanCreateRootWorkspaces = false, string AccessVersion = "0");
