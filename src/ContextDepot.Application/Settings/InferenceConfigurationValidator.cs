using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Inferences;

namespace ContextDepot.Application.Settings;

public static class InferenceConfigurationValidator
{
    public static SaveInferenceProviderRequest NormalizeProvider(SaveInferenceProviderRequest request)
    {
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
        return request with
        {
            Name = request.Name.Trim(), Endpoint = endpoint.AbsoluteUri,
            ApiKey = string.IsNullOrWhiteSpace(request.ApiKey) ? null : request.ApiKey.Trim(),
            Embedding = request.Embedding is { } embedding ? embedding with { Model = embedding.Model.Trim() } : null,
            Chat = request.Chat is { } chat ? chat with { Model = chat.Model.Trim() } : null
        };
    }

    private static bool ValidModel(InferenceProviderModelRequest? model, bool embedding) => model is null ||
        !string.IsNullOrWhiteSpace(model.Model) && model.Model.Trim().Length <= 300 &&
        model.TimeoutSeconds is >= 1 and <= 300 &&
        (embedding ? model.Dimensions is > 0 and <= 16000 : model.Dimensions is null);

    public static string? PublicEndpoint(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) || endpoint.Scheme is not ("http" or "https")) return null;
        return new UriBuilder(endpoint) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.AbsoluteUri;
    }
}
