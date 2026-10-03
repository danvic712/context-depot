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
        if (!InferenceProviderKinds.IsSupported(request.Kind) ||
            request.Embedding is not null && !InferenceProviderKinds.SupportsEmbedding(request.Kind) ||
            string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200 ||
            request.Id is not null && request.UpdatedAt is null ||
            string.IsNullOrWhiteSpace(request.Endpoint) || request.Endpoint.Length > 2000 ||
            !Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !InferenceProviderKinds.IsValidEndpoint(request.Kind, endpoint) ||
            !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment) ||
            request.ApiKey?.Length > 8192 || !ValidModel(request.Embedding, true) || !ValidModel(request.Chat, false))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidInferenceConfiguration);
        return repository.SaveProviderAsync(request with
        {
            Name = request.Name.Trim(), Endpoint = endpoint.AbsoluteUri,
            ApiKey = string.IsNullOrWhiteSpace(request.ApiKey) ? null : request.ApiKey.Trim(),
            Embedding = request.Embedding is { } embedding ? embedding with { Model = embedding.Model.Trim() } : null,
            Chat = request.Chat is { } chat ? chat with { Model = chat.Model.Trim() } : null
        }, cancellationToken);
    }

    private static bool ValidModel(InferenceProviderModelRequest? model, bool embedding) => model is null ||
        !string.IsNullOrWhiteSpace(model.Model) && model.Model.Trim().Length <= 300 &&
        model.TimeoutSeconds is >= 1 and <= 300 &&
        (embedding ? model.Dimensions is > 0 and <= 16000 : model.Dimensions is null);

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
