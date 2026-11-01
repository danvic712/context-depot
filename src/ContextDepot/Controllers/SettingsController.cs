using ContextDepot.Application.Settings;
using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ContextDepot.Controllers;

/// <summary>
/// Provides application settings, model service configuration and access key management.
/// </summary>
/// <param name="accessKeys">
/// The application service used to manage access keys and their workspace permissions.
/// </param>
/// <param name="inference">
/// The application service used to manage model routes and provider connections.
/// </param>
/// <param name="overview">
/// The application service used to load the settings overview.
/// </param>
/// <param name="agentSessions">
/// The application service used to read and update session memory settings.
/// </param>
[ApiController]
[Route("api/settings")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SettingsController(IAccessKeyAppService accessKeys, InferenceSettingsAppService inference, SettingsOverviewAppService overview, AgentSessionSettingsAppService agentSessions) : ControllerBase
{
    /// <summary>
    /// Reads the application-wide session memory setting.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// The stored session memory setting.
    /// </returns>
    [HttpGet("agent-sessions")]
    public async Task<ActionResult<AgentSessionSettingsDto>> GetAgentSessionsAsync(CancellationToken cancellationToken) =>
        Ok(await agentSessions.GetAsync(cancellationToken));

    /// <summary>
    /// Updates whether session memory is enabled for the application.
    /// </summary>
    /// <param name="request">
    /// The explicit enabled or disabled value to save.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// The stored setting after saving and refreshing the current configuration.
    /// </returns>
    [HttpPut("agent-sessions")]
    public async Task<ActionResult<AgentSessionSettingsDto>> SaveAgentSessionsAsync(SaveAgentSessionSettingsRequest request, CancellationToken cancellationToken) =>
        Ok(await agentSessions.SaveAsync(request, cancellationToken));

    /// <summary>
    /// Loads the storage, search and connection information displayed on the settings page.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// The settings overview for the selected depot.
    /// </returns>
    [HttpGet("overview")]
    public async Task<ActionResult<SettingsOverviewDto>> GetOverviewAsync(CancellationToken cancellationToken) =>
        Ok(await overview.GetAsync(cancellationToken));

    /// <summary>
    /// Lists access keys and the workspace permissions available for management.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// Access key information without full key secrets.
    /// </returns>
    [HttpGet("access-keys")]
    public async Task<ActionResult<AccessKeyListDto>> ListKeysAsync(CancellationToken cancellationToken) =>
        Ok(await accessKeys.ListAsync(cancellationToken));

    /// <summary>
    /// Creates an access key with the requested name and workspace permissions.
    /// </summary>
    /// <param name="request">
    /// The key name, workspace grants and permission to create spaces.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// The created key information and its full secret, available only when issued.
    /// </returns>
    [HttpPost("access-keys")]
    public async Task<ActionResult<IssuedAccessKeyDto>> CreateKeyAsync(CreateAccessKeyRequest request, CancellationToken cancellationToken) =>
        Ok(await accessKeys.CreateAsync(request, cancellationToken));

    /// <summary>
    /// Disables an access key so it can no longer authorize requests.
    /// </summary>
    /// <param name="id">
    /// The identifier of the key to disable.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// The disabled key information.
    /// </returns>
    [HttpPost("access-keys/{id:guid}/revoke")]
    public async Task<ActionResult<AccessKeyDto>> RevokeKeyAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(await accessKeys.RevokeAsync(id, cancellationToken));

    /// <summary>
    /// Replaces an access key while preserving its workspace permissions.
    /// </summary>
    /// <param name="id">
    /// The identifier of the key to replace.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// The replacement key information and its newly issued full secret.
    /// </returns>
    [HttpPost("access-keys/{id:guid}/rotate")]
    public async Task<ActionResult<IssuedAccessKeyDto>> RotateKeyAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(await accessKeys.RotateAsync(id, cancellationToken));

    /// <summary>
    /// Updates an access key's workspace grants and permission to create spaces.
    /// </summary>
    /// <param name="id">
    /// The identifier of the key to update.
    /// </param>
    /// <param name="request">
    /// The desired permissions and expected access version for concurrency checking.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// The key information with its updated permissions.
    /// </returns>
    [HttpPut("access-keys/{id:guid}/grants")]
    public async Task<ActionResult<AccessKeyDto>> SetGrantsAsync(Guid id, UpdateAccessKeyGrantsRequest request, CancellationToken cancellationToken) =>
        Ok(await accessKeys.SetGrantsAsync(id, request, cancellationToken));

    /// <summary>
    /// Lists the configured model routes for search and conversation capabilities.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// The model routes and their current configuration.
    /// </returns>
    [HttpGet("inference")]
    public async Task<ActionResult<IReadOnlyList<InferenceRouteDto>>> GetInferenceAsync(CancellationToken cancellationToken) =>
        Ok(await inference.GetAsync(cancellationToken));

    /// <summary>
    /// Saves the model route for a supported capability.
    /// </summary>
    /// <param name="capability">
    /// The capability whose model route is being configured.
    /// </param>
    /// <param name="request">
    /// The provider connection, model and capability-specific settings to save.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// The saved model route.
    /// </returns>
    [HttpPut("inference/{capability}")]
    public async Task<ActionResult<InferenceRouteDto>> SaveInferenceAsync(string capability, SaveInferenceRouteRequest request, CancellationToken cancellationToken) =>
        Ok(await inference.SaveAsync(capability, request, cancellationToken));

    /// <summary>
    /// Loads model provider connections and the available provider presets.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// Provider settings without plaintext credentials.
    /// </returns>
    [HttpGet("inference/providers")]
    public async Task<ActionResult<InferenceProviderSettingsDto>> GetProvidersAsync(CancellationToken cancellationToken) =>
        Ok(await inference.GetProvidersAsync(cancellationToken));

    /// <summary>
    /// Saves a model provider connection and its supported model settings.
    /// </summary>
    /// <param name="request">
    /// The provider identity, connection details, credentials and model settings to save.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels request processing.
    /// </param>
    /// <returns>
    /// The refreshed provider settings without plaintext credentials.
    /// </returns>
    [HttpPut("inference/providers")]
    public async Task<ActionResult<InferenceProviderSettingsDto>> SaveProviderAsync(SaveInferenceProviderRequest request, CancellationToken cancellationToken) =>
        Ok(await inference.SaveProviderAsync(request, cancellationToken));
}
