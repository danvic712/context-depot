using Microsoft.Extensions.Configuration;

namespace ContextDepot.Infrastructure.RuntimeConfiguration;

public sealed class DatabaseApplicationSettingsConfigurationProvider : ConfigurationProvider
{
    public bool HasPublishedSnapshot { get; private set; }

    public override void Load()
    {
        Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        HasPublishedSnapshot = false;
    }

    public bool HasSameSnapshot(IReadOnlyDictionary<string, string?> candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!HasPublishedSnapshot || Data.Count != candidate.Count)
        {
            return false;
        }

        return candidate.All(pair =>
            Data.TryGetValue(pair.Key, out var currentValue) &&
            string.Equals(currentValue, pair.Value, StringComparison.Ordinal));
    }

    public void Publish(IReadOnlyDictionary<string, string?> snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Data = new Dictionary<string, string?>(snapshot, StringComparer.OrdinalIgnoreCase);
        HasPublishedSnapshot = true;
        OnReload();
    }
}