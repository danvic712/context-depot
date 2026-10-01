using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using FluentValidation;

namespace ContextDepot.Application.Settings;

public sealed class AppearanceSettingsAppService(
    IAppearanceSettingsRepository repository,
    IValidator<ThemeRequest> themeValidator,
    IValidator<LanguageRequest> languageValidator)
    : IAppearanceSettingsAppService
{
    public Task<AppearanceSettingsDto> GetAsync(CancellationToken cancellationToken) =>
        repository.GetAsync(cancellationToken);

    public async Task SetThemeAsync(ThemeRequest request, CancellationToken cancellationToken)
    {
        await themeValidator.ValidateAndThrowAsync(request, cancellationToken);
        await repository.SetThemeAsync(request.Theme!.Value, cancellationToken);
    }

    public async Task SetLanguageAsync(LanguageRequest request, CancellationToken cancellationToken)
    {
        await languageValidator.ValidateAndThrowAsync(request, cancellationToken);
        await repository.SetLanguageAsync(request.Language!.Value, cancellationToken);
    }
}
