namespace ContextDepot.Application.Workspaces.Enums;

/// <summary>
/// Defines supported rolling update windows, measured in days.
/// </summary>
public enum WorkspaceUpdateWindow
{
    /// <summary>
    /// Includes updates during the preceding seven days.
    /// </summary>
    Week = 7,

    /// <summary>
    /// Includes updates during the preceding thirty days.
    /// </summary>
    Month = 30,

    /// <summary>
    /// Includes updates during the preceding ninety days.
    /// </summary>
    Quarter = 90
}
