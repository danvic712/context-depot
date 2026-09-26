using Microsoft.Extensions.Configuration;

namespace ContextDepot.Infrastructure.RuntimeConfiguration;

public sealed class DatabaseApplicationSettingsConfigurationSource : IConfigurationSource
{
    public DatabaseApplicationSettingsConfigurationProvider? Provider { get; private set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        Provider = new DatabaseApplicationSettingsConfigurationProvider();
}