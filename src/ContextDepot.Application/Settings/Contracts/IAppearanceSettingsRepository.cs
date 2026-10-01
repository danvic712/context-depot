using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Settings.Enums;

namespace ContextDepot.Application.Settings.Contracts;

public interface IAppearanceSettingsRepository
{
    Task<AppearanceSettingsDto> GetAsync(CancellationToken cancellationToken);

    Task SetThemeAsync(Theme theme, CancellationToken cancellationToken);

    Task SetLanguageAsync(Language language, CancellationToken cancellationToken);
}
