namespace ContextDepot.Application.Overview;

/// <summary>
/// Describes a visible workspace and its current content counts.
/// </summary>
/// <param name="Id">
/// The workspace identifier.
/// </param>
/// <param name="Name">
/// The workspace display name.
/// </param>
/// <param name="Description">
/// The optional workspace description.
/// </param>
/// <param name="Path">
/// The full workspace path, including its ancestors.
/// </param>
/// <param name="ContextCount">
/// The number of contexts directly owned by this workspace and retrievable at the observation time.
/// </param>
/// <param name="DocumentCount">
/// The number of active documents directly owned by this workspace.
/// </param>
/// <param name="ActivityAt">
/// The latest update time among the workspace and its included content.
/// </param>
public sealed record WorkspaceSummary(Guid Id, string Name, string? Description, string Path,
    int ContextCount, int DocumentCount, DateTimeOffset ActivityAt);
