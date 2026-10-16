using ContextDepot.Application.Overview;
using ContextDepot.Application.Documents;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ContextDepot.Controllers;

/// <summary>
/// Provides authorized workspace browsing, knowledge listings and workspace creation.
/// </summary>
/// <param name="service">
/// The application service used for recent collections and workspace creation.
/// </param>
/// <param name="browser">
/// The application service used for authorized workspace browsing.
/// </param>
/// <param name="documents">
/// The application service used for trusted archive management.
/// </param>
[ApiController]
[Route("api/workspaces")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WorkspacesController(OverviewAppService service, WorkspaceBrowserAppService browser,
    DocumentManagementAppService documents) : ControllerBase
{
    /// <summary>
    /// Finds spaces matching the requested conditions within the current authorization scope.
    /// </summary>
    /// <param name="request">
    /// Scope, keyword, content filters, update bounds, ordering and pagination.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// The requested page and total number of matching spaces.
    /// </returns>
    /// <remarks>
    /// Filters are applied before counting and pagination. Date endpoints are inclusive at the start and exclusive at the end.
    /// </remarks>
    [HttpGet("browse")]
    public async Task<ActionResult<WorkspaceDirectory>> BrowseAsync([FromQuery] WorkspaceDirectoryQuery request,
        CancellationToken cancellationToken) => Ok(await browser.BrowseAsync(request, cancellationToken));

    /// <summary>
    /// Opens a workspace visible to the current caller.
    /// </summary>
    /// <param name="id">
    /// The workspace identifier.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// The workspace information and navigable ancestors.
    /// </returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkspaceDetail>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(await browser.GetAsync(id, cancellationToken));

    /// <summary>
    /// Lists valid contexts and active documents saved directly to a workspace.
    /// </summary>
    /// <param name="id">
    /// The workspace identifier.
    /// </param>
    /// <param name="request">
    /// The one-based pagination request.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// A page of knowledge with its total count.
    /// </returns>
    [HttpGet("{id:guid}/knowledge")]
    public async Task<ActionResult<WorkspaceKnowledge>> ListKnowledgeAsync(Guid id, [FromQuery] PaginationQuery request,
        CancellationToken cancellationToken) => Ok(await browser.ListKnowledgeAsync(id, request, cancellationToken));

    /// <summary>
    /// Lists archived canonical documents in the selected workspace.
    /// </summary>
    /// <param name="id">
    /// The workspace identifier in the selected depot.
    /// </param>
    /// <param name="request">
    /// The one-based pagination request supplied in the query string.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task whose result contains a consistent archive page and its total count.
    /// </returns>
    /// <remarks>
    /// Requires trusted Web management access; archived documents are excluded from ordinary retrieval.
    /// </remarks>
    /// <response code="200">
    /// The archive page was loaded.
    /// </response>
    /// <response code="400">
    /// The pagination request is invalid.
    /// </response>
    /// <response code="403">
    /// Trusted Web management access is required.
    /// </response>
    /// <response code="404">
    /// The workspace does not exist in the current scope.
    /// </response>
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("{id:guid}/archived-documents")]
    public async Task<ActionResult<ArchivedDocumentPage>> ListArchivedDocumentsAsync(Guid id, [FromQuery] PaginationQuery request,
        CancellationToken cancellationToken)
    {
        Middlewares.WebManagementRequestGuard.RequireManagementRequest(HttpContext);
        return Ok(await documents.ListArchivedAsync(id, request, cancellationToken));
    }

    /// <summary>
    /// Lists recently active workspaces for the home overview.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <param name="limit">
    /// The number of spaces requested, from 1 to 8.
    /// </param>
    /// <param name="sort">
    /// The supported overview order, descending activity time.
    /// </param>
    /// <returns>
    /// The recent workspaces and an indication of whether more are available.
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<ResourceCollection<WorkspaceSummary>>> ListAsync(
        CancellationToken cancellationToken, [FromQuery] int limit = 3, [FromQuery] string sort = "-activityAt") =>
        Ok(await service.ListWorkspacesAsync(limit, sort, cancellationToken));

    /// <summary>
    /// Creates a workspace under the current depot.
    /// </summary>
    /// <param name="request">
    /// The workspace name, path and optional description.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// The created workspace and its resource location.
    /// </returns>
    [HttpPost]
    public async Task<ActionResult<WorkspaceSummary>> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateWorkspaceAsync(request, cancellationToken);
        return Created($"/api/workspaces/{result.Id}", result);
    }
}
