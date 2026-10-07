using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Setup.Dtos;

namespace ContextDepot.Application.Setup.Contracts;

public interface ISetupAppService
{
    Task<SetupStatusDto> GetAsync(CancellationToken cancellationToken);

    Task<InferenceProviderSettingsDto> GetInferenceSettingsAsync(CancellationToken cancellationToken);

    Task<SetupCompletionDto> CompleteAsync(CompleteSetupRequest request, CancellationToken cancellationToken);
}
