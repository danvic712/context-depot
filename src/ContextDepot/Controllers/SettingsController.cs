using ContextDepot.Application.Settings;
using ContextDepot.Application.Settings.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ContextDepot.Controllers;

[ApiController]
[Route("api/settings")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SettingsController(AccessKeyAppService accessKeys, InferenceSettingsAppService inference, SettingsOverviewAppService overview) : ControllerBase
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

    [HttpGet("inference")]
    public async Task<ActionResult<IReadOnlyList<InferenceRouteDto>>> GetInferenceAsync(CancellationToken cancellationToken) =>
        Ok(await inference.GetAsync(cancellationToken));

    [HttpPut("inference/{capability}")]
    public async Task<ActionResult<InferenceRouteDto>> SaveInferenceAsync(string capability, SaveInferenceRouteRequest request, CancellationToken cancellationToken) =>
        Ok(await inference.SaveAsync(capability, request, cancellationToken));

    [HttpGet("inference/providers")]
    public async Task<ActionResult<InferenceProviderSettingsDto>> GetProvidersAsync(CancellationToken cancellationToken) =>
        Ok(await inference.GetProvidersAsync(cancellationToken));

    [HttpPut("inference/providers")]
    public async Task<ActionResult<InferenceProviderSettingsDto>> SaveProviderAsync(SaveInferenceProviderRequest request, CancellationToken cancellationToken) =>
        Ok(await inference.SaveProviderAsync(request, cancellationToken));
}
