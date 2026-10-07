namespace ContextDepot.Application.Settings.Dtos;

public sealed record SettingsOverviewDto(string DepotName, string DatabaseState, string MarkdownState,
    string SemanticState, string IndexState, int? IndexedCount, int? TotalCount, string McpPath);
