namespace ContextDepot.Application.Overview;

/// <summary>
/// Identifies the workspace that owns a knowledge resource.
/// </summary>
/// <param name="Id">
/// The workspace identifier.
/// </param>
/// <param name="Path">
/// The full workspace path, including its ancestors.
/// </param>
public sealed record KnowledgeWorkspace(Guid Id, string Path);
