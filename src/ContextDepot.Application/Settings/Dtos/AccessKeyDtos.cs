namespace ContextDepot.Application.Settings.Dtos;

public sealed record AccessKeyDto(Guid Id, string Name, string Prefix, DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt, IReadOnlyList<Guid> WorkspaceIds);

public sealed record AccessKeyWorkspaceDto(Guid Id, string Name, string Path);
public sealed record AccessKeyListDto(IReadOnlyList<AccessKeyDto> Items, IReadOnlyList<AccessKeyWorkspaceDto> Workspaces);
public sealed record CreateAccessKeyRequest(string Name, Guid[] WorkspaceIds);
public sealed record UpdateAccessKeyGrantsRequest(Guid[] WorkspaceIds);
public sealed record IssuedAccessKeyDto(AccessKeyDto Key, string Secret)
{
    public override string ToString() => $"{nameof(IssuedAccessKeyDto)} {{ Secret = [REDACTED] }}";
}
