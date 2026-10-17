using ContextDepot.Application.Shared.Runtime;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Shared.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;

namespace ContextDepot.Application.Documents;

/// <summary>
/// Provides trusted archive browsing and revision-checked permanent document deletion.
/// </summary>
/// <param name="depot">
/// The server-selected depot context.
/// </param>
/// <param name="access">
/// The current workspace access scope.
/// </param>
/// <param name="workspaces">
/// The service used to resolve and authorize the owning workspace.
/// </param>
/// <param name="documents">
/// The repository used to resolve document metadata within the selected depot.
/// </param>
/// <param name="repository">
/// The repository that lists archived documents and performs permanent deletion.
/// </param>
/// <param name="coordinator">
/// The coordinator that serializes deletion with canonical document writes.
/// </param>
public sealed class DocumentManagementAppService(ICurrentDepotContext depot, IWorkspaceAccessContext access,
    IWorkspaceAppService workspaces, IDocumentRepository documents, IDocumentManagementRepository repository,
    DocumentWriteCoordinator coordinator)
{
    /// <summary>
    /// Lists archived documents after validating management access, pagination and workspace visibility.
    /// </summary>
    /// <param name="workspaceId">
    /// The workspace to browse.
    /// </param>
    /// <param name="query">
    /// The one-based pagination request.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task whose result contains a consistent archive page and its total count.
    /// </returns>
    /// <exception cref="ContextDepotApplicationException">
    /// Management access is denied, pagination is invalid, or the workspace does not exist in the current scope.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before completion.
    /// </exception>
    public async Task<ArchivedDocumentPage> ListArchivedAsync(Guid workspaceId, PaginationQuery query, CancellationToken cancellationToken)
    {
        ManagementAccess.RequireManagement(depot, access);
        if (!query.IsValid()) throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidResourceQuery);
        _ = await workspaces.GetAsync(workspaceId, cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);
        return await repository.ListArchivedAsync(depot.DepotId, workspaceId, query, cancellationToken);
    }

    /// <summary>
    /// Serializes permanent document deletion with canonical writes and verifies the observed revision.
    /// </summary>
    /// <param name="documentId">
    /// The active or archived document identifier.
    /// </param>
    /// <param name="expectedUpdatedAt">
    /// The exact revision obtained from the reader.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels processing before canonical content is removed.
    /// </param>
    /// <returns>
    /// A task that completes after canonical content, metadata, chunks and every vector profile are removed.
    /// </returns>
    /// <remarks>
    /// The canonical path is resolved on the server. The write lease is held until deletion completes.
    /// After canonical content is removed, database commit and any required file restoration are not canceled by client disconnection.
    /// </remarks>
    /// <exception cref="ContextDepotApplicationException">
    /// Management access is denied, the revision is missing or stale, the resource or workspace is missing,
    /// or the canonical file cannot be deleted.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before completion.
    /// </exception>
    public async Task DeleteAsync(Guid documentId, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken)
    {
        ManagementAccess.RequireManagement(depot, access);
        if (expectedUpdatedAt == default) throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidResourceQuery);
        var document = await documents.GetByIdAsync(depot.DepotId, documentId, cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.DocumentNotFound);
        var workspace = await workspaces.GetAsync(document.WorkspaceId, cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);
        if (document.UpdatedAt != expectedUpdatedAt)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.DocumentConflict);
        await using var writeLock = await coordinator.AcquireAsync(depot.DepotId + ":" + workspace.Id + ":" + document.Path, cancellationToken);
        try
        {
            await repository.DeleteAsync(depot.DepotId, documentId, workspace.Id, workspace.Path + "/" + document.Path,
                expectedUpdatedAt, cancellationToken);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.DocumentDeleteFailed);
        }
    }
}
