using ContextDepot.Application.AgentSessions;
using ContextDepot.Infrastructure.Repositories;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Tests;

public sealed class AgentSessionSettingsRepositoryTests
{
    [PostgreSqlFact]
    public async Task Upgrade_defaults_disabled_then_saves_refreshes_and_retains_authoritative_boolean()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync("20261020231901_AddAgentSessions");
        await using var db = fixture.CreateDb();
        Assert.False(await db.ApplicationSettings.AnyAsync(s => s.Key == "AgentSessions:Enabled"));
        await db.Database.MigrateAsync();
        var source = new DatabaseApplicationSettingsConfigurationSource();
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["AgentSessions:Enabled"] = "true" });
        ((IConfigurationBuilder)configuration).Add(source);
        var services = new ServiceCollection();
        services.AddScoped(_ => fixture.CreateDb());
        services.AddOptions<SessionCaptureOptions>().Bind(configuration.GetSection("AgentSessions"));
        using var provider = services.BuildServiceProvider();
        var loader = new DatabaseApplicationSettingsSnapshotLoader(provider.GetRequiredService<IServiceScopeFactory>(),
            source.Provider!, new(), NullLogger<DatabaseApplicationSettingsSnapshotLoader>.Instance);
        await loader.RefreshAsync(default);
        var monitor = provider.GetRequiredService<IOptionsMonitor<SessionCaptureOptions>>();
        var repository = new AgentSessionSettingsRepository(db, TimeProvider.System, loader);
        Assert.False((await repository.GetAsync(default)).Enabled);
        Assert.False(monitor.CurrentValue.Enabled);
        foreach (var enabled in new[] { true, false, true })
        {
            await repository.SetEnabledAsync(enabled, default);
            Assert.Equal(enabled, (await repository.GetAsync(default)).Enabled);
            Assert.Equal(enabled, monitor.CurrentValue.Enabled);
            await using var restartedDb = fixture.CreateDb();
            Assert.Equal(enabled ? "true" : "false", await restartedDb.ApplicationSettings.AsNoTracking()
                .Where(s => s.Key == "AgentSessions:Enabled").Select(s => s.ValueJson).SingleAsync());
        }
    }
}
