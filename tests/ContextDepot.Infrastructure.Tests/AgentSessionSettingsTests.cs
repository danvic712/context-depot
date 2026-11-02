using ContextDepot.Application.AgentSessions;
using ContextDepot.Domain.Settings;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Infrastructure.Migrations;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Tests;

public sealed class AgentSessionSettingsTests
{
    private static ApplicationSetting[] Records(string? enabled = "false")
    {
        var initial = new AddDatabaseManagedConfiguration().UpOperations.OfType<InsertDataOperation>()
            .Single(operation => operation.Table == "application_settings");
        var records = Enumerable.Range(0, initial.Values.GetLength(0)).Select(row => new ApplicationSetting
        {
            Id = (Guid)initial.Values[row, 0]!, Key = ((string)initial.Values[row, 1]!)["ContextDepot:".Length..],
            UpdatedAt = (DateTimeOffset)initial.Values[row, 2]!, ValueJson = (string)initial.Values[row, 3]!
        }).ToList();
        if (enabled is not null) records.Add(new() { Key = "AgentSessions:Enabled", ValueJson = enabled });
        return records.ToArray();
    }

    [Fact]
    public void Snapshot_maps_short_database_keys_to_existing_configuration_paths()
    {
        var records = Records();
        records.Single(record => record.Key == "Appearance:Theme").ValueJson = "\"dark\"";
        records.Single(record => record.Key == "Retrieval:Search:DefaultLimit").ValueJson = "17";
        var snapshot = new DatabaseApplicationSettingsSnapshotBuilder().Build(records);

        Assert.Equal(15, snapshot.Count);
        Assert.Equal("dark", snapshot["ContextDepot:Appearance:Theme"]);
        Assert.Equal("17", snapshot["ContextDepot:Retrieval:Search:DefaultLimit"]);
        Assert.Equal("30", snapshot["ContextDepot:IndexRepair:PollIntervalSeconds"]);
        Assert.Equal("30", snapshot["ContextDepot:VectorCoverage:CacheDurationSeconds"]);
        Assert.False(snapshot.ContainsKey("Appearance:Theme"));
        Assert.Equal("false", snapshot["AgentSessions:Enabled"]);
    }

    [Fact]
    public void Snapshot_rejects_unmigrated_prefixed_database_keys()
    {
        var records = Records();
        records[0].Key = "ContextDepot:" + records[0].Key;
        Assert.Equal("ApplicationSettingKeyUnsupported", Assert.Throws<InvalidOperationException>(() =>
            new DatabaseApplicationSettingsSnapshotBuilder().Build(records)).Message);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void Snapshot_accepts_json_boolean(string value) =>
        Assert.Equal(value, new DatabaseApplicationSettingsSnapshotBuilder().Build(Records(value))["AgentSessions:Enabled"]);

    [Theory]
    [InlineData("\"true\"")]
    [InlineData("\"false\"")]
    [InlineData("1")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Snapshot_rejects_non_boolean_enabled(string value) =>
        Assert.Equal("ApplicationSettingValueInvalid", Assert.Throws<InvalidOperationException>(() =>
            new DatabaseApplicationSettingsSnapshotBuilder().Build(Records(value))).Message);

    [Fact]
    public void Snapshot_requires_enabled_key() =>
        Assert.Equal("ApplicationSettingsMissing", Assert.Throws<InvalidOperationException>(() =>
            new DatabaseApplicationSettingsSnapshotBuilder().Build(Records(null))).Message);

    [Fact]
    public void Migration_is_discoverable_and_inserts_json_false_independent_of_environment()
    {
        using var db = CreateDb();
        Assert.Contains("20261021000000_AddAgentSessionSettings", db.Database.GetMigrations());
        var insert = Assert.Single(new AddAgentSessionSettings().UpOperations.OfType<InsertDataOperation>());
        Assert.Equal("AgentSessions:Enabled", insert.Values[0, 1]);
        Assert.Equal("false", insert.Values[0, 3]);
        Assert.Equal("jsonb", insert.ColumnTypes![3]);
        Assert.Single(new AddAgentSessionSettings().DownOperations.OfType<DeleteDataOperation>());
        Assert.False(db.Database.HasPendingModelChanges());
        var sql = db.GetService<IMigrator>().GenerateScript("20261020231901_AddAgentSessions", "20261021000000_AddAgentSessionSettings");
        Assert.Contains("AgentSessions:Enabled", sql);
        Assert.Contains("false", sql);
        Assert.DoesNotContain("CREATE TABLE", sql);
    }

    [Fact]
    public void Database_provider_overrides_legacy_enabled_and_reloads_monitor_without_overriding_queue_tuning()
    {
        const string prefix = "CONTEXTDEPOT_SESSION_SETTINGS_TEST_";
        Environment.SetEnvironmentVariable(prefix + "AgentSessions__Enabled", "true");
        Environment.SetEnvironmentVariable(prefix + "AgentSessions__PollIntervalSeconds", "17");
        try
        {
            var source = new DatabaseApplicationSettingsConfigurationSource();
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables(prefix).Add(source).Build();
            using var configurationLifetime = (IDisposable)configuration;
            var provider = source.Provider!;
            provider.Publish(new DatabaseApplicationSettingsSnapshotBuilder().Build(Records()));
            var services = new ServiceCollection();
            services.AddOptions<SessionCaptureOptions>().Bind(configuration.GetSection("AgentSessions"));
            using var container = services.BuildServiceProvider();
            var monitor = container.GetRequiredService<IOptionsMonitor<SessionCaptureOptions>>();
            Assert.False(monitor.CurrentValue.Enabled);
            Assert.Equal(17, monitor.CurrentValue.PollIntervalSeconds);
            provider.Publish(new DatabaseApplicationSettingsSnapshotBuilder().Build(Records("true")));
            Assert.True(monitor.CurrentValue.Enabled);
            provider.Publish(new DatabaseApplicationSettingsSnapshotBuilder().Build(Records()));
            Assert.False(monitor.CurrentValue.Enabled);
            Assert.Equal(17, monitor.CurrentValue.PollIntervalSeconds);
        }
        finally
        {
            Environment.SetEnvironmentVariable(prefix + "AgentSessions__Enabled", null);
            Environment.SetEnvironmentVariable(prefix + "AgentSessions__PollIntervalSeconds", null);
        }
    }

    private static ContextDepotDbContext CreateDb() => new(
        new DbContextOptionsBuilder<ContextDepotDbContext>().UseNpgsql("Host=localhost;Database=unused").Options,
        new CurrentDepotAccessContext());
}
