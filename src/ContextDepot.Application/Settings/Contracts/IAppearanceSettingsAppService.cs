using ContextDepot.Application.Settings.Dtos;

namespace ContextDepot.Application.Settings.Contracts;

public interface IAppearanceSettingsAppService
{
    Task<AppearanceSettingsDto> GetAsync(CancellationToken cancellationToken);

    Task SetThemeAsync(ThemeRequest request, CancellationToken cancellationToken);

    Task SetLanguageAsync(LanguageRequest request, CancellationToken cancellationToken);
}
