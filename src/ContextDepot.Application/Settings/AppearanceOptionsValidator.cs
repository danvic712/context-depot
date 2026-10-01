using System.Globalization;
using Microsoft.Extensions.Options;
using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Application.Settings;

public sealed class AppearanceOptionsValidator : IValidateOptions<AppearanceOptions>
{
    public ValidateOptionsResult Validate(string? name, AppearanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            if (string.IsNullOrWhiteSpace(options.Language))
            {
                return ValidateOptionsResult.Fail(ApplicationErrorCodes.InvalidAppearanceLanguage);
            }

            _ = CultureInfo.GetCultureInfo(options.Language);
        }
        catch (CultureNotFoundException)
        {
            return ValidateOptionsResult.Fail(ApplicationErrorCodes.InvalidAppearanceLanguage);
        }

        return options.Theme is "system" or "light" or "dark"
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(ApplicationErrorCodes.InvalidAppearanceTheme);
    }
}
