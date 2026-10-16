namespace ContextDepot.Application.Overview;

/// <summary>
/// Contains canonical knowledge content and the revision used for optimistic concurrency.
/// </summary>
/// <param name="Id">
/// The context or document identifier.
/// </param>
/// <param name="Type">
/// The resource type: context or document.
/// </param>
/// <param name="Title">
/// The display title, including the resource-specific fallback when no title was saved.
/// </param>
/// <param name="Workspace">
/// The full path of the workspace that owns the resource.
/// </param>
/// <param name="Content">
/// The canonical content to render as Markdown.
/// </param>
/// <param name="UpdatedAt">
/// The precise observed revision required for revision-checked deletion.
/// </param>
public sealed record KnowledgePreview(Guid Id, string Type, string Title, string Workspace,
    string Content, DateTimeOffset UpdatedAt);
