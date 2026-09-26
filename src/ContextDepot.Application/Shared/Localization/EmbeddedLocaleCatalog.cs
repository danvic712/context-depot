using System.Reflection;
using System.Text.Json;

namespace ContextDepot.Application.Shared.Localization;

public sealed class EmbeddedLocaleCatalog
{
    public const string DefaultLocale = "zh-CN";

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> messagesByLocale;

    public EmbeddedLocaleCatalog(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        messagesByLocale = LoadMessages(assembly);
    }

    public bool Contains(string messageKey) =>
        messagesByLocale.Values.Any(messages => messages.ContainsKey(messageKey));

    public IReadOnlyDictionary<string, string> GetMessages(string? locale)
    {
        var normalizedLocale = NormalizeLocale(locale);
        return messagesByLocale.TryGetValue(normalizedLocale, out var messages)
            ? messages
            : messagesByLocale[DefaultLocale];
    }

    private static string NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return DefaultLocale;
        }

        var normalizedLocale = locale.Trim().Replace('_', '-');
        return normalizedLocale.Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? "zh-CN"
            : normalizedLocale.Equals("en", StringComparison.OrdinalIgnoreCase)
                ? "en-US"
                : normalizedLocale;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadMessages(Assembly assembly)
    {
        var resourceNames = assembly
            .GetManifestResourceNames()
            .Where(static resourceName => resourceName.Contains(".locales.", StringComparison.OrdinalIgnoreCase)
                && resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static resourceName => resourceName, StringComparer.Ordinal)
            .ToArray();

        var messagesByLocale = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var resourceName in resourceNames)
        {
            var locale = GetLocale(resourceName);
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(nameof(EmbeddedLocaleCatalog));
            var localizedMessages = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                ?? throw new InvalidOperationException(nameof(EmbeddedLocaleCatalog));
            if (!messagesByLocale.TryGetValue(locale, out var messages))
            {
                messages = new Dictionary<string, string>(StringComparer.Ordinal);
                messagesByLocale.Add(locale, messages);
            }

            foreach (var localizedMessage in localizedMessages)
            {
                if (!messages.TryAdd(localizedMessage.Key, localizedMessage.Value))
                {
                    throw new InvalidOperationException(nameof(EmbeddedLocaleCatalog));
                }
            }
        }

        return messagesByLocale.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyDictionary<string, string>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string GetLocale(string resourceName)
    {
        const string localeMarker = ".locales.";
        var markerIndex = resourceName.IndexOf(localeMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            throw new InvalidOperationException(nameof(EmbeddedLocaleCatalog));
        }

        var localeStart = markerIndex + localeMarker.Length;
        var localeEnd = resourceName.IndexOf('.', localeStart);
        if (localeEnd <= localeStart)
        {
            throw new InvalidOperationException(nameof(EmbeddedLocaleCatalog));
        }

        return NormalizeLocale(resourceName[localeStart..localeEnd]);
    }
}
