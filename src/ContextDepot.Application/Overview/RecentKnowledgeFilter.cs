using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Overview;

/// <summary>Restricts recent knowledge before ordering and limiting the collection.</summary>
/// <param name="Workspace">An optional exact workspace path.</param>
/// <param name="Type">The validated contexts or documents choice, or null for both.</param>
/// <param name="Kind">An optional context category, which excludes documents.</param>
public sealed record RecentKnowledgeFilter(string? Workspace, string? Type, ContextKind? Kind);
