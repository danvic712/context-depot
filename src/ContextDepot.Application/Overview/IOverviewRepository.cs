namespace ContextDepot.Application.Overview;

/// <summary>
/// Loads depot-scoped overview collections and creates workspaces without overwriting existing ones.
/// </summary>
public interface IOverviewRepository
{
    /// <summary>
    /// Lists recently active workspaces in the selected depot at the requested observation time.
    /// </summary>
    /// <param name="depotId">
    /// The server-selected depot identifier.
    /// </param>
    /// <param name="limit">
    /// The validated maximum number of items to return.
    /// </param>
    /// <param name="asOf">
    /// The observation time used to evaluate context visibility.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task whose result contains the requested workspaces, observation time and continuation indicator.
    /// </returns>
    /// <remarks>
    /// The caller is responsible for authorizing depot-wide overview access before invoking the repository.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before completion.
    /// </exception>
    Task<ResourceCollection<WorkspaceSummary>> ListWorkspacesAsync(Guid depotId, int limit, DateTimeOffset asOf, CancellationToken cancellationToken);

    /// <summary>
    /// Lists retrievable contexts and active documents in the selected depot.
    /// </summary>
    /// <param name="depotId">
    /// The server-selected depot identifier.
    /// </param>
    /// <param name="limit">
    /// The validated maximum number of items to return.
    /// </param>
    /// <param name="asOf">
    /// The observation time used to evaluate context visibility.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task whose result contains recent knowledge, observation time and continuation indicator.
    /// </returns>
    /// <remarks>
    /// The caller is responsible for authorizing depot-wide overview access before invoking the repository.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before completion.
    /// </exception>
    Task<ResourceCollection<KnowledgeSummary>> ListKnowledgeAsync(Guid depotId, int limit, DateTimeOffset asOf, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a root workspace without updating an existing workspace.
    /// </summary>
    /// <param name="depotId">
    /// The owning depot identifier.
    /// </param>
    /// <param name="id">
    /// The identifier assigned to the new workspace.
    /// </param>
    /// <param name="name">
    /// The validated workspace display name.
    /// </param>
    /// <param name="path">
    /// The validated root path slug.
    /// </param>
    /// <param name="description">
    /// The optional validated description.
    /// </param>
    /// <param name="now">
    /// The timestamp used for creation and initial activity.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task whose result is true after creation, or false when the depot already contains the root path.
    /// </returns>
    /// <remarks>
    /// A path conflict never overwrites an existing workspace.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before completion.
    /// </exception>
    Task<bool> CreateWorkspaceAsync(Guid depotId, Guid id, string name, string path, string? description, DateTimeOffset now, CancellationToken cancellationToken);
}
