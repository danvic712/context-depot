using ContextDepot.Application.Overview;
using Microsoft.AspNetCore.Mvc;

namespace ContextDepot.Controllers;

[ApiController]
[Route("api/knowledge")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class KnowledgeController(OverviewAppService service, KnowledgeSearchAppService search) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ResourceCollection<KnowledgeSummary>>> ListAsync(
        CancellationToken cancellationToken, [FromQuery] int limit = 3, [FromQuery] string sort = "-updatedAt") =>
        Ok(await service.ListKnowledgeAsync(limit, sort, cancellationToken));

    [HttpGet("search")]
    public async Task<ActionResult<KnowledgeSearchResponse>> SearchAsync(
        CancellationToken cancellationToken, [FromQuery] string? query = null, [FromQuery] string? workspace = null,
        [FromQuery] string? kind = null) =>
        Ok(await search.SearchAsync(query ?? string.Empty, workspace, cancellationToken, kind));

    [HttpGet("workspaces")]
    public async Task<ActionResult<IReadOnlyList<KnowledgeWorkspace>>> WorkspacesAsync(CancellationToken cancellationToken) =>
        Ok(await search.ListWorkspacesAsync(cancellationToken));

    [HttpGet("{type}/{id:guid}")]
    public async Task<ActionResult<KnowledgePreview>> GetAsync(string type, Guid id, CancellationToken cancellationToken) =>
        Ok(await search.GetAsync(type, id, cancellationToken));
}
