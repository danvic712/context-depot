using ContextDepot.Application.Shared.Localization;

namespace ContextDepot.Application.Shared.Exceptions;

public static class ApplicationErrorMessages
{
    private static readonly EmbeddedLocaleCatalog Catalog = new(typeof(ApplicationErrorMessages).Assembly);

    public static string Get(string errorCode, string? locale = null)
    {
        var messages = Catalog.GetMessages(locale);
        return messages.TryGetValue(errorCode, out var message)
            ? message
            : Catalog.GetMessages(EmbeddedLocaleCatalog.DefaultLocale)[ApplicationErrorCodes.InternalError];
    }

    public static bool Contains(string errorCode) => Catalog.Contains(errorCode);
}
