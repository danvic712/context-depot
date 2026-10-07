using ContextDepot.Application.Settings.Dtos;

namespace ContextDepot.Application.Setup.Contracts;

public interface ISetupRepository
{
    Task<InferenceProviderSettingsDto> GetInferenceSettingsAsync(CancellationToken cancellationToken);

    Task<SetupSnapshot> GetAsync(CancellationToken cancellationToken);

    // The callback runs under the installation row lock. Application owns lifecycle rules;
    // the repository atomically persists state changes and any newly created resources.
    Task<SetupSnapshot> MutateAsync(Func<SetupSnapshot, SetupResources?> mutation, CancellationToken cancellationToken);
}
