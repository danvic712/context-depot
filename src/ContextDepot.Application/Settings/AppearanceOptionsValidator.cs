using System.Globalization;
using Microsoft.Extensions.Options;

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
                return ValidateOptionsResult.Fail("Appearance language is required.");
            }

            _ = CultureInfo.GetCultureInfo(options.Language);
        }
        catch (CultureNotFoundException)
        {
            return ValidateOptionsResult.Fail("Appearance language must be a valid culture name.");
        }

        return options.Theme is "system" or "light" or "dark"
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Appearance theme must be system, light or dark.");
    }
}
