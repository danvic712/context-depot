using ContextDepot.Application.Settings;
using ContextDepot.Application.Settings.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ContextDepot.Controllers;

[ApiController]
[Route("api/settings")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SettingsController(AccessKeyAppService accessKeys, AiSettingsAppService ai, SettingsOverviewAppService overview) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<SettingsOverviewDto>> GetOverviewAsync(CancellationToken cancellationToken) =>
        Ok(await overview.GetAsync(cancellationToken));

    [HttpGet("access-keys")]
    public async Task<ActionResult<AccessKeyListDto>> ListKeysAsync(CancellationToken cancellationToken) =>
        Ok(await accessKeys.ListAsync(cancellationToken));

    [HttpPost("access-keys")]
    public async Task<ActionResult<IssuedAccessKeyDto>> CreateKeyAsync(CreateAccessKeyRequest request, CancellationToken cancellationToken) =>
        Ok(await accessKeys.CreateAsync(request, cancellationToken));

    [HttpPost("access-keys/{id:guid}/revoke")]
    public async Task<ActionResult<AccessKeyDto>> RevokeKeyAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(await accessKeys.RevokeAsync(id, cancellationToken));

    [HttpPost("access-keys/{id:guid}/rotate")]
    public async Task<ActionResult<IssuedAccessKeyDto>> RotateKeyAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(await accessKeys.RotateAsync(id, cancellationToken));

    [HttpPut("access-keys/{id:guid}/grants")]
    public async Task<ActionResult<AccessKeyDto>> SetGrantsAsync(Guid id, UpdateAccessKeyGrantsRequest request, CancellationToken cancellationToken) =>
        Ok(await accessKeys.SetGrantsAsync(id, request, cancellationToken));

    [HttpGet("ai")]
    public async Task<ActionResult<IReadOnlyList<AiRouteDto>>> GetAiAsync(CancellationToken cancellationToken) =>
        Ok(await ai.GetAsync(cancellationToken));

    [HttpPut("ai/{capability}")]
    public async Task<ActionResult<AiRouteDto>> SaveAiAsync(string capability, SaveAiRouteRequest request, CancellationToken cancellationToken) =>
        Ok(await ai.SaveAsync(capability, request, cancellationToken));

    [HttpGet("ai/providers")]
    public async Task<ActionResult<AiProviderSettingsDto>> GetProvidersAsync(CancellationToken cancellationToken) =>
        Ok(await ai.GetProvidersAsync(cancellationToken));

    [HttpPut("ai/providers")]
    public async Task<ActionResult<AiProviderSettingsDto>> SaveProviderAsync(SaveAiProviderRequest request, CancellationToken cancellationToken) =>
        Ok(await ai.SaveProviderAsync(request, cancellationToken));
}
