namespace ContextDepot.Application.Overview;

/// <summary>
/// Carries the values used to create a root workspace.
/// </summary>
/// <param name="Name">
/// The display name to validate and trim before creation.
/// </param>
/// <param name="Path">
/// The root path slug to validate and trim before creation.
/// </param>
/// <param name="Description">
/// The optional description; blank values are treated as absent.
/// </param>
public sealed record CreateWorkspaceRequest(string? Name, string? Path, string? Description);
