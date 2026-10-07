using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Domain.Inferences;

namespace ContextDepot.Application.Settings;

public sealed class InferenceSettingsAppService(IInferenceSettingsRepository repository, ICurrentDepotContext depot, IWorkspaceAccessContext access)
{
    public Task<IReadOnlyList<InferenceRouteDto>> GetAsync(CancellationToken cancellationToken)
    {
        SettingsAccess.RequireManagement(depot, access);
        return repository.GetAsync(cancellationToken);
    }

    public Task<InferenceProviderSettingsDto> GetProvidersAsync(CancellationToken cancellationToken)
    {
        SettingsAccess.RequireManagement(depot, access);
        return repository.GetProvidersAsync(cancellationToken);
    }

    public Task<InferenceProviderSettingsDto> SaveProviderAsync(SaveInferenceProviderRequest request, CancellationToken cancellationToken)
    {
        SettingsAccess.RequireManagement(depot, access);
        return repository.SaveProviderAsync(InferenceConfigurationValidator.NormalizeProvider(request), cancellationToken);
    }

    public Task<InferenceRouteDto> SaveAsync(string capability, SaveInferenceRouteRequest request, CancellationToken cancellationToken)
    {
        SettingsAccess.RequireManagement(depot, access);
        if (capability is not ("embedding" or "chat") || request.TimeoutSeconds is < 1 or > 300 ||
            (request.ProviderId is null
                ? request.Model is not null || request.Dimensions is not null
                : request.ProviderId == Guid.Empty || request.ProviderUpdatedAt is null ||
                  string.IsNullOrWhiteSpace(request.Model) || request.Model.Trim().Length > 300 ||
                  (capability == "embedding" && request.Dimensions is not (> 0 and <= 16000)) ||
                  (capability == "chat" && request.Dimensions is not null)))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidInferenceConfiguration);
        return repository.SaveAsync(capability, request with { Model = request.Model?.Trim() }, cancellationToken);
    }
}
