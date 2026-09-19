using ContextDepot.Application.Bootstrap.Enums;

namespace ContextDepot.Application.Bootstrap.Dtos;

public sealed record ScopeResolution(ScopeResolutionStatus Status, IReadOnlyList<string> Workspaces);
