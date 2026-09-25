using ContextDepot.Domain.Localization;

namespace ContextDepot.Application.Shared.Exceptions;

public static class ApplicationErrorMessages
{
    private const string LocaleEnvironmentVariable = "CONTEXT_DEPOT_LOCALE";
    private static readonly EmbeddedLocaleCatalog Catalog = new(typeof(ApplicationErrorMessages).Assembly);

    public static string Get(string errorCode)
    {
        var messages = Catalog.GetMessages(Environment.GetEnvironmentVariable(LocaleEnvironmentVariable));
        return messages.TryGetValue(errorCode, out var message)
            ? message
            : Catalog.GetMessages(EmbeddedLocaleCatalog.DefaultLocale)[ApplicationErrorCodes.InternalError];
    }

    public static bool Contains(string errorCode) => Catalog.Contains(errorCode);
}
