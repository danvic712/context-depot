using System.Globalization;
using ContextDepot.Domain.Shared.Localization;

namespace ContextDepot.Domain.Shared;

public static class DomainErrorMessages
{
    private const string LocaleEnvironmentVariable = "CONTEXT_DEPOT_LOCALE";
    private static readonly EmbeddedLocaleCatalog Catalog = new(typeof(DomainErrorMessages).Assembly);

    public static string ExpectedEnumValue(string enumName) =>
        string.Format(
            CultureInfo.InvariantCulture,
            Catalog.GetMessages(Environment.GetEnvironmentVariable(LocaleEnvironmentVariable))[nameof(ExpectedEnumValue)],
            enumName);
}
