using ContextDepot.Application.Shared.Dtos;
using ContextDepot.Application.Workspaces.Enums;

namespace ContextDepot.Application.Workspaces.Dtos;

/// <summary>
/// Contains the scope, filters, ordering and pagination of a workspace directory request.
/// </summary>
public sealed record WorkspaceDirectoryQuery : PaginationQuery
{
    /// <summary>
    /// The maximum number of characters in a workspace keyword.
    /// </summary>
    public const int MaximumQueryLength = 200;

    /// <summary>
    /// Gets an optional parent scope. When omitted, all authorized spaces are included.
    /// </summary>
    public Guid? ParentId { get; init; }

    /// <summary>
    /// Gets a literal, case-insensitive keyword for names, complete paths and descriptions.
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// Gets the content availability filter. Defaults to all spaces.
    /// </summary>
    public WorkspaceContentStatus Status { get; init; } = WorkspaceContentStatus.All;

    /// <summary>
    /// Gets the knowledge types a space must contain. Defaults to no restriction.
    /// </summary>
    public WorkspaceContentType Content { get; init; } = WorkspaceContentType.All;

    /// <summary>
    /// Gets an optional rolling update window. Cannot be combined with absolute date bounds.
    /// </summary>
    public WorkspaceUpdateWindow? UpdatedWithinDays { get; init; }

    /// <summary>
    /// Gets the inclusive earliest update timestamp.
    /// </summary>
    public DateTimeOffset? UpdatedFrom { get; init; }

    /// <summary>
    /// Gets the exclusive latest update timestamp.
    /// </summary>
    public DateTimeOffset? UpdatedBefore { get; init; }

    /// <summary>
    /// Gets the directory ordering. Ties are resolved by workspace ID.
    /// </summary>
    public WorkspaceSortOrder Sort { get; init; } = WorkspaceSortOrder.Activity;
}
