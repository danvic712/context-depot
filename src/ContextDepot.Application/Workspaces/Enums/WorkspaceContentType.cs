namespace ContextDepot.Application.Workspaces.Enums;

/// <summary>
/// Specifies the types of knowledge a space must contain.
/// </summary>
public enum WorkspaceContentType
{
    /// <summary>
    /// Does not restrict the types of saved knowledge.
    /// </summary>
    All,

    /// <summary>
    /// Requires at least one visible context.
    /// </summary>
    Contexts,

    /// <summary>
    /// Requires at least one active document.
    /// </summary>
    Documents,

    /// <summary>
    /// Requires both visible contexts and active documents.
    /// </summary>
    Both
}
