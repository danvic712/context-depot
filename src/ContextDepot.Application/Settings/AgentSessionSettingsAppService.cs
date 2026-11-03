using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Validation;
using FluentValidation;

namespace ContextDepot.Application.Settings;

public sealed class AgentSessionSettingsAppService(
    IAgentSessionSettingsRepository repository,
    IValidator<SaveAgentSessionSettingsRequest> validator)
{
    public Task<AgentSessionSettingsDto> GetAsync(CancellationToken cancellationToken) =>
        repository.GetAsync(cancellationToken);

    public async Task<AgentSessionSettingsDto> SaveAsync(SaveAgentSessionSettingsRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateBusinessRulesAsync(request, cancellationToken);
        await repository.SetEnabledAsync(request.Enabled!.Value, cancellationToken);
        return await repository.GetAsync(cancellationToken);
    }
}
