namespace ContextDepot.Application.Overview;

/// <summary>
/// Describes one distinct context or document in a ranked knowledge search result.
/// </summary>
/// <param name="Id">
/// The context or document identifier.
/// </param>
/// <param name="Type">
/// The resource type: context or document.
/// </param>
/// <param name="Title">
/// The display title with a fallback when no title was saved.
/// </param>
/// <param name="Workspace">
/// The full path of the owning workspace.
/// </param>
/// <param name="Excerpt">
/// The content excerpt returned by retrieval.
/// </param>
/// <param name="Kind">
/// The lowercase context kind, or document for a document hit.
/// </param>
public sealed record KnowledgeSearchHit(Guid Id, string Type, string Title, string Workspace, string Excerpt, string Kind);
