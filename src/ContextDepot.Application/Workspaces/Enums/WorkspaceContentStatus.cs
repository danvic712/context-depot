namespace ContextDepot.Application.Workspaces.Enums;

/// <summary>
/// Filters spaces by whether they contain visible, active knowledge.
/// </summary>
public enum WorkspaceContentStatus
{
    /// <summary>
    /// Includes spaces regardless of their content.
    /// </summary>
    All,

    /// <summary>
    /// Includes spaces with at least one context or document.
    /// </summary>
    Populated,

    /// <summary>
    /// Includes spaces with no visible, active contexts or documents.
    /// </summary>
    Empty
}
