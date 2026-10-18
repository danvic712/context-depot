using ContextDepot.Application.Overview;
using ContextDepot.Application.Documents.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ContextDepot.Controllers;

/// <summary>
/// Provides authorized knowledge search, workspace scopes and canonical content previews.
/// </summary>
/// <param name="service">
/// The application service used for recent knowledge collections.
/// </param>
/// <param name="search">
/// The application service used for authorized search and canonical previews.
/// </param>
[ApiController]
[Route("api/knowledge")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class KnowledgeController(OverviewAppService service, KnowledgeSearchAppService search) : ControllerBase
{
    /// <summary>
    /// Resolves a Markdown link within the active source document's authorized workspace.
    /// </summary>
    /// <param name="documents">The authorized canonical document service.</param>
    /// <param name="id">The source document identifier.</param>
    /// <param name="path">The URL-encoded Markdown path, without query or fragment.</param>
    /// <param name="workspaceId">An optional owning-workspace constraint.</param>
    /// <param name="cancellationToken">Cancels the asynchronous operation.</param>
    /// <returns>The active target document identifier.</returns>
    [HttpGet("document/{id:guid}/link")]
    public async Task<ActionResult<object>> ResolveDocumentLinkAsync([FromServices] IDocumentAppService documents,
        Guid id, [FromQuery] string path, CancellationToken cancellationToken, [FromQuery] Guid? workspaceId = null) =>
        Ok(new { id = await documents.ResolveLinkAsync(id, path, cancellationToken, workspaceId) });

    /// <summary>
    /// Lists recent knowledge for the home overview.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <param name="limit">
    /// The number of items requested, from 1 to 20.
    /// </param>
    /// <param name="sort">
    /// The supported overview order: -updatedAt.
    /// </param>
    /// <returns>
    /// A task whose result contains the recent knowledge collection and continuation indicator.
    /// </returns>
    /// <response code="200">
    /// The recent knowledge collection was loaded.
    /// </response>
    /// <response code="400">
    /// The limit or sort order is unsupported.
    /// </response>
    [HttpGet]
    public async Task<ActionResult<ResourceCollection<KnowledgeSummary>>> ListAsync(
        CancellationToken cancellationToken, [FromQuery] int limit = 3, [FromQuery] string sort = "-updatedAt") =>
        Ok(await service.ListKnowledgeAsync(limit, sort, cancellationToken));

    /// <summary>
    /// Searches authorized contexts and documents with optional workspace and context-kind filters.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <param name="query">
    /// The optional search text; an omitted value is passed as an empty string.
    /// </param>
    /// <param name="workspace">
    /// An optional workspace path used to restrict retrieval.
    /// </param>
    /// <param name="kind">
    /// An optional context kind; selecting a kind excludes documents.
    /// </param>
    /// <returns>
    /// A task whose result contains distinct ranked hits, the effective limit and retrieval availability.
    /// </returns>
    /// <remarks>
    /// Workspace grants are enforced by retrieval. A degraded response remains successful and exposes the degradation indicator.
    /// </remarks>
    /// <response code="200">
    /// The search completed, possibly with degraded retrieval.
    /// </response>
    /// <response code="400">
    /// A search parameter is invalid.
    /// </response>
    [HttpGet("search")]
    public async Task<ActionResult<KnowledgeSearchResponse>> SearchAsync(
        CancellationToken cancellationToken, [FromQuery] string? query = null, [FromQuery] string? workspace = null,
        [FromQuery] string? kind = null) =>
        Ok(await search.SearchAsync(query ?? string.Empty, workspace, cancellationToken, kind));

    /// <summary>Lists recently updated knowledge within the search page's current filters.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <param name="workspace">An optional exact workspace path.</param>
    /// <param name="type">All, contexts or documents.</param>
    /// <param name="kind">An optional context category.</param>
    /// <param name="limit">The collection limit, from one to twenty.</param>
    /// <returns>The ordered recent collection and continuation indicator.</returns>
    [HttpGet("recent")]
    public async Task<ActionResult<ResourceCollection<KnowledgeSummary>>> RecentAsync(CancellationToken cancellationToken,
        [FromQuery] string? workspace = null, [FromQuery] string? type = null, [FromQuery] string? kind = null,
        [FromQuery] int limit = 20) =>
        Ok(await service.ListRecentKnowledgeAsync(limit, workspace, type, kind, cancellationToken));

    /// <summary>
    /// Lists the caller's visible workspace scopes for search.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task whose result contains readable workspace identifiers, display names and full paths in path order.
    /// </returns>
    /// <response code="200">
    /// The visible search scopes were loaded.
    /// </response>
    [HttpGet("workspaces")]
    public async Task<ActionResult<IReadOnlyList<KnowledgeSearchWorkspace>>> WorkspacesAsync(CancellationToken cancellationToken) =>
        Ok(await search.ListWorkspacesAsync(cancellationToken));

    /// <summary>
    /// Reads canonical context or document content visible to the current caller.
    /// </summary>
    /// <param name="type">
    /// The resource type: context or document.
    /// </param>
    /// <param name="id">
    /// The resource identifier.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task whose result contains canonical content and its precise observed revision.
    /// </returns>
    /// <response code="200">
    /// The canonical preview was loaded.
    /// </response>
    /// <response code="400">
    /// The resource type is unsupported.
    /// </response>
    /// <response code="404">
    /// The resource is missing.
    /// </response>
    [HttpGet("{type}/{id:guid}")]
    public async Task<ActionResult<KnowledgePreview>> GetAsync(string type, Guid id, CancellationToken cancellationToken) =>
        Ok(await search.GetAsync(type, id, cancellationToken));

    /// <summary>
    /// Reads canonical knowledge content only when it belongs to the requested workspace.
    /// </summary>
    /// <param name="workspaceId">
    /// The workspace identifier that must own the resource.
    /// </param>
    /// <param name="type">
    /// The resource type: context or document.
    /// </param>
    /// <param name="id">
    /// The resource identifier.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task whose result contains canonical content and its precise observed revision.
    /// </returns>
    /// <remarks>
    /// Missing resources and resources outside the requested workspace both return not found.
    /// </remarks>
    /// <response code="200">
    /// The canonical preview was loaded.
    /// </response>
    /// <response code="400">
    /// The resource type is unsupported.
    /// </response>
    /// <response code="404">
    /// The resource is missing or does not belong to the requested workspace.
    /// </response>
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("/api/workspaces/{workspaceId:guid}/knowledge/{type}/{id:guid}")]
    public async Task<ActionResult<KnowledgePreview>> GetInWorkspaceAsync(Guid workspaceId, string type, Guid id, CancellationToken cancellationToken) =>
        Ok(await search.GetAsync(type, id, cancellationToken, workspaceId));
}
