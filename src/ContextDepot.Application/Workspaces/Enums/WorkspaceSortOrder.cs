namespace ContextDepot.Application.Workspaces.Enums;

/// <summary>
/// Specifies the ordering of a workspace directory.
/// </summary>
public enum WorkspaceSortOrder
{
    /// <summary>
    /// Places recently updated spaces first.
    /// </summary>
    Activity,

    /// <summary>
    /// Orders spaces by name, ascending.
    /// </summary>
    Name,

    /// <summary>
    /// Places spaces with more visible knowledge first.
    /// </summary>
    Knowledge
}
