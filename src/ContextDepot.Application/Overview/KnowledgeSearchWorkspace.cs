namespace ContextDepot.Application.Overview;

/// <summary>
/// Describes an authorized search scope with a display name and its stable path.
/// </summary>
/// <param name="Id">The workspace identifier used for navigation.</param>
/// <param name="Name">The workspace name shown to people.</param>
/// <param name="Path">The exact path used to scope retrieval requests.</param>
public sealed record KnowledgeSearchWorkspace(Guid Id, string Name, string Path);
