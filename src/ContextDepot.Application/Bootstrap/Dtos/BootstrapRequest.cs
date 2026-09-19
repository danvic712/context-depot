namespace ContextDepot.Application.Bootstrap.Dtos;

public sealed record BootstrapRequest(
    string Query,
    IReadOnlyList<string>? Workspaces = null,
    int MaxTokens = 1200);
