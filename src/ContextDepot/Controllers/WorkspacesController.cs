using ContextDepot.Application.Overview;
using Microsoft.AspNetCore.Mvc;

namespace ContextDepot.Controllers;

[ApiController]
[Route("api/workspaces")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WorkspacesController(OverviewAppService service) : ControllerBase
{
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
