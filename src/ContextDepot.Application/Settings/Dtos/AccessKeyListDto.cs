namespace ContextDepot.Application.Settings.Dtos;

public sealed record AccessKeyListDto(IReadOnlyList<AccessKeyDto> Items, IReadOnlyList<AccessKeyWorkspaceDto> Workspaces);
