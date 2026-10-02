using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;

namespace ContextDepot.Application.Settings;

public sealed class AiSettingsAppService(IAiSettingsRepository repository, ICurrentDepotContext depot, IWorkspaceAccessContext access)
{
    public Task<IReadOnlyList<AiRouteDto>> GetAsync(CancellationToken cancellationToken)
    {
        SettingsAccess.RequireManagement(depot, access);
        return repository.GetAsync(cancellationToken);
    }

    public Task<AiRouteDto> SaveAsync(string capability, SaveAiRouteRequest request, CancellationToken cancellationToken)
    {
        SettingsAccess.RequireManagement(depot, access);
        if (capability is not ("embedding" or "chat") ||
            string.IsNullOrWhiteSpace(request.ProviderName) || request.ProviderName.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(request.Model) || request.Model.Trim().Length > 300 ||
            string.IsNullOrWhiteSpace(request.Endpoint) || request.Endpoint.Length > 2000 ||
            !Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment) ||
            request.TimeoutSeconds is < 1 or > 300 ||
            (capability == "embedding" && request.Dimensions is not (> 0 and <= 16000)) ||
            (capability == "chat" && request.Dimensions is not null) ||
            (request.ApiKey is not null && request.ApiKey.Length > 8192))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidAiConfiguration);
        return repository.SaveAsync(capability, request with
        {
            ProviderName = request.ProviderName.Trim(), Endpoint = endpoint.AbsoluteUri,
            Model = request.Model.Trim(), ApiKey = string.IsNullOrWhiteSpace(request.ApiKey) ? null : request.ApiKey.Trim()
        }, cancellationToken);
    }
}
