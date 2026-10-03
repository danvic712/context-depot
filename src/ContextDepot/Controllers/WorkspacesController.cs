using ContextDepot.Application.Overview;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ContextDepot.Controllers;

[ApiController]
[Route("api/workspaces")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WorkspacesController(OverviewAppService service, WorkspaceBrowserAppService browser) : ControllerBase
{
    [HttpGet("browse")]
    public async Task<ActionResult<WorkspaceDirectory>> BrowseAsync(CancellationToken cancellationToken,
        [FromQuery] Guid? parentId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 12) =>
        Ok(await browser.BrowseAsync(parentId, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkspaceDetail>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(await browser.GetAsync(id, cancellationToken));

    [HttpGet("{id:guid}/knowledge")]
    public async Task<ActionResult<WorkspaceKnowledge>> ListKnowledgeAsync(Guid id, CancellationToken cancellationToken,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 12) =>
        Ok(await browser.ListKnowledgeAsync(id, page, pageSize, cancellationToken));

    [HttpGet]
    public async Task<ActionResult<ResourceCollection<WorkspaceSummary>>> ListAsync(
        CancellationToken cancellationToken, [FromQuery] int limit = 3, [FromQuery] string sort = "-activityAt") =>
        Ok(await service.ListWorkspacesAsync(limit, sort, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<WorkspaceSummary>> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateWorkspaceAsync(request, cancellationToken);
        return Created($"/api/workspaces/{result.Id}", result);
    }
}
