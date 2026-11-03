using ContextDepot.Application;
using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.IndexRepair;
using ContextDepot.Application.Shared.Exceptions;
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

        Assert.Equal(14, snapshot.Count);
        Assert.Equal("dark", snapshot["ContextDepot:Appearance:Theme"]);
        Assert.Equal("17", snapshot["ContextDepot:Retrieval:Search:DefaultLimit"]);
        Assert.False(snapshot.ContainsKey("ContextDepot:IndexRepair:PollIntervalSeconds"));
        Assert.False(snapshot.ContainsKey("IndexRepair:PollIntervalSeconds"));
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
    public void Snapshot_accepts_missing_legacy_index_interval()
    {
        var records = Records().Where(record => record.Key != "IndexRepair:PollIntervalSeconds");

        var snapshot = new DatabaseApplicationSettingsSnapshotBuilder().Build(records);

        Assert.Equal(14, snapshot.Count);
        Assert.False(snapshot.ContainsKey("ContextDepot:IndexRepair:PollIntervalSeconds"));
    }

    [Theory]
    [InlineData("IndexRepair:PollIntervalSeconds", "30")]
    [InlineData("indexrepair:pollintervalseconds", "not-json")]
    [InlineData("INDEXREPAIR:POLLINTERVALSECONDS", "null")]
    [InlineData("IndexRepair:PollIntervalSeconds", "{}")]
    [InlineData("IndexRepair:PollIntervalSeconds", "[]")]
    [InlineData("IndexRepair:PollIntervalSeconds", "-1")]
    public void Snapshot_ignores_legacy_index_interval_without_parsing_or_publishing(string key, string value)
    {
        // Keep the seeded legacy record too: obsolete records do not participate in duplicate checks.
        var records = Records().Append(new ApplicationSetting { Key = key, ValueJson = value });

        var snapshot = new DatabaseApplicationSettingsSnapshotBuilder().Build(records);

        Assert.Equal(14, snapshot.Count);
        Assert.DoesNotContain(snapshot.Keys, item => item.Contains("PollIntervalSeconds", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("IndexRepair:Unknown")]
    [InlineData("ContextDepot:IndexRepair:PollIntervalSeconds")]
    [InlineData("IndexRepair:PollIntervalSeconds:Child")]
    [InlineData("IndexRepair:PollIntervalSecondsExtra")]
    [InlineData("IndexRepair:PollIntervalSeconds ")]
    public void Snapshot_rejects_unknown_keys_before_parsing_values(string key)
    {
        var records = Records().Append(new ApplicationSetting { Key = key, ValueJson = "not-json" });

        Assert.Equal("ApplicationSettingKeyUnsupported", Assert.Throws<InvalidOperationException>(() =>
            new DatabaseApplicationSettingsSnapshotBuilder().Build(records)).Message);
    }

    [Fact]
    public void Snapshot_rejects_duplicate_supported_keys_case_insensitively()
    {
        var records = Records().Append(new ApplicationSetting { Key = "indexrepair:batchsize", ValueJson = "32" });

        Assert.Equal("ApplicationSettingKeyDuplicated", Assert.Throws<InvalidOperationException>(() =>
            new DatabaseApplicationSettingsSnapshotBuilder().Build(records)).Message);
    }

    [Theory]
    [InlineData("IndexRepair:BatchSize")]
    [InlineData("IndexRepair:MaxBatchesPerCycle")]
    public void Snapshot_requires_remaining_index_keys(string key)
    {
        var records = Records().Where(record => record.Key != key);

        Assert.Equal("ApplicationSettingsMissing", Assert.Throws<InvalidOperationException>(() =>
            new DatabaseApplicationSettingsSnapshotBuilder().Build(records)).Message);
    }

    [Fact]
    public void Snapshot_rejects_invalid_json_for_supported_keys()
    {
        var records = Records();
        records.Single(record => record.Key == "IndexRepair:BatchSize").ValueJson = "not-json";

        Assert.Equal("ApplicationSettingJsonInvalid", Assert.Throws<InvalidOperationException>(() =>
            new DatabaseApplicationSettingsSnapshotBuilder().Build(records)).Message);
    }

    [Theory]
    [InlineData("IndexRepair:BatchSize", "0")]
    [InlineData("IndexRepair:BatchSize", "257")]
    [InlineData("IndexRepair:MaxBatchesPerCycle", "0")]
    [InlineData("IndexRepair:MaxBatchesPerCycle", "101")]
    public void Snapshot_rejects_invalid_index_batch_settings(string key, string value)
    {
        var records = Records();
        records.Single(record => record.Key == key).ValueJson = value;

        Assert.Equal("IndexRepairSettingsInvalid", Assert.Throws<InvalidOperationException>(() =>
            new DatabaseApplicationSettingsSnapshotBuilder().Build(records)).Message);
    }

    [Theory]
    [InlineData(false, "30")]
    [InlineData(true, "30")]
    [InlineData(false, "not-an-integer")]
    [InlineData(true, "not-an-integer")]
    public void External_legacy_index_interval_is_ignored(bool useEnvironment, string value)
    {
        const string prefix = "CONTEXTDEPOT_LEGACY_INDEX_SETTINGS_TEST_";
        const string variable = prefix + "ContextDepot__IndexRepair__PollIntervalSeconds";
        try
        {
            var builder = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ContextDepot:IndexRepair:BatchSize"] = "7",
                ["ContextDepot:IndexRepair:MaxBatchesPerCycle"] = "2"
            });
            if (useEnvironment)
            {
                Environment.SetEnvironmentVariable(variable, value);
                builder.AddEnvironmentVariables(prefix);
            }
            else
            {
                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    ContextDepot = new { IndexRepair = new { PollIntervalSeconds = value } }
                });
                builder.AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)));
            }

            var configuration = builder.Build();
            using var configurationLifetime = (IDisposable)configuration;
            var services = new ServiceCollection();
            services.AddContextDepotApplication(configuration);
            using var container = services.BuildServiceProvider();

            var options = container.GetRequiredService<IOptions<IndexRepairOptions>>().Value;
            Assert.Equal(7, options.BatchSize);
            Assert.Equal(2, options.MaxBatchesPerCycle);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Theory]
    [InlineData("BatchSizeTypo")]
    [InlineData("PollIntervalSecondsExtra")]
    public void External_index_configuration_still_rejects_unknown_keys(string key)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ContextDepot:IndexRepair:PollIntervalSeconds"] = "ignored",
            ["ContextDepot:IndexRepair:" + key] = "7"
        }).Build();
        using var configurationLifetime = (IDisposable)configuration;
        var services = new ServiceCollection();
        services.AddContextDepotApplication(configuration);
        using var container = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            container.GetRequiredService<IOptions<IndexRepairOptions>>().Value);
        Assert.Contains(key, exception.Message);
    }

    [Theory]
    [InlineData("BatchSize", "0")]
    [InlineData("BatchSize", "257")]
    [InlineData("MaxBatchesPerCycle", "0")]
    [InlineData("MaxBatchesPerCycle", "101")]
    public void External_index_configuration_still_validates_batches_with_legacy_interval(string key, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ContextDepot:IndexRepair:PollIntervalSeconds"] = "ignored",
            ["ContextDepot:IndexRepair:" + key] = value
        }).Build();
        using var configurationLifetime = (IDisposable)configuration;
        var services = new ServiceCollection();
        services.AddContextDepotApplication(configuration);
        using var container = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            container.GetRequiredService<IOptions<IndexRepairOptions>>().Value);
        Assert.Contains(ApplicationErrorCodes.IndexRepairConfigurationInvalid, exception.Failures);
    }

    [Fact]
    public void Index_options_reload_after_filtering_legacy_interval()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ContextDepot:IndexRepair:PollIntervalSeconds"] = "ignored",
            ["ContextDepot:IndexRepair:BatchSize"] = "7",
            ["ContextDepot:IndexRepair:MaxBatchesPerCycle"] = "2"
        }).Build();
        using var configurationLifetime = (IDisposable)configuration;
        var services = new ServiceCollection();
        services.AddContextDepotApplication(configuration);
        using var container = services.BuildServiceProvider();
        var monitor = container.GetRequiredService<IOptionsMonitor<IndexRepairOptions>>();
        Assert.Equal(7, monitor.CurrentValue.BatchSize);
        Assert.Equal(2, monitor.CurrentValue.MaxBatchesPerCycle);

        configuration["ContextDepot:IndexRepair:BatchSize"] = "13";
        configuration["ContextDepot:IndexRepair:MaxBatchesPerCycle"] = "3";
        configuration["ContextDepot:IndexRepair:PollIntervalSeconds"] = "still-ignored";
        configuration.Reload();

        Assert.Equal(13, monitor.CurrentValue.BatchSize);
        Assert.Equal(3, monitor.CurrentValue.MaxBatchesPerCycle);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("301")]
    [InlineData("not-an-integer")]
    public void External_legacy_capture_interval_is_ignored(string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AgentSessions:PollIntervalSeconds"] = value,
            ["AgentSessions:MaxAttempts"] = "4"
        }).Build();
        using var configurationLifetime = (IDisposable)configuration;
        var services = new ServiceCollection();
        services.AddContextDepotApplication(configuration);
        using var container = services.BuildServiceProvider();

        var options = container.GetRequiredService<IOptions<SessionCaptureOptions>>().Value;

        Assert.Equal(4, options.MaxAttempts);
        Assert.False(options.Enabled);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(256, 100)]
    public void Index_batch_validation_accepts_boundaries(int batchSize, int maxBatches)
    {
        var result = new IndexRepairOptionsValidator().Validate(null, new IndexRepairOptions
        {
            BatchSize = batchSize,
            MaxBatchesPerCycle = maxBatches
        });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("MaxAttempts", "0")]
    [InlineData("MaxAttempts", "6")]
    [InlineData("RetentionHours", "0")]
    [InlineData("RetentionHours", "169")]
    [InlineData("MaxCallsPerHour", "0")]
    [InlineData("MaxCallsPerHour", "301")]
    [InlineData("MaxPendingEventsPerKey", "1")]
    [InlineData("MaxPendingEventsPerKey", "10001")]
    [InlineData("ExtractionTimeoutSeconds", "0")]
    [InlineData("ExtractionTimeoutSeconds", "121")]
    public void Capture_validation_rejects_remaining_out_of_range_settings(string key, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AgentSessions:" + key] = value
        }).Build();
        using var configurationLifetime = (IDisposable)configuration;
        var services = new ServiceCollection();
        services.AddContextDepotApplication(configuration);
        using var container = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            container.GetRequiredService<IOptions<SessionCaptureOptions>>().Value);
        Assert.Contains(ApplicationErrorCodes.InvalidSessionCapture, exception.Failures);
    }

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
    public void Obsolete_index_interval_migration_is_discoverable_and_only_changes_setting_data()
    {
        const string previous = "20261101153238_AddAutomaticCaptureRoutingAndEvidence";
        const string current = "20261102000000_RemoveIndexRepairPollInterval";
        using var db = CreateDb();
        Assert.Contains(current, db.Database.GetMigrations());
        Assert.False(db.Database.HasPendingModelChanges());
        var migration = new RemoveIndexRepairPollInterval();
        var deletion = Assert.IsType<SqlOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("DELETE FROM public.application_settings\nWHERE normalized_key = 'indexrepair:pollintervalseconds';",
            deletion.Sql.Replace("\r\n", "\n"));
        var restoration = Assert.IsType<SqlOperation>(Assert.Single(migration.DownOperations));
        Assert.Contains("'IndexRepair:PollIntervalSeconds'", restoration.Sql);
        Assert.Contains("'30'::jsonb", restoration.Sql);
        Assert.Contains("ON CONFLICT DO NOTHING", restoration.Sql);

        var migrator = db.GetService<IMigrator>();
        var upgrade = migrator.GenerateScript(previous, current);
        var downgrade = migrator.GenerateScript(current, previous);
        Assert.Contains(deletion.Sql, upgrade);
        Assert.Contains(restoration.Sql, downgrade);
        Assert.DoesNotContain("CREATE TABLE", upgrade);
        Assert.DoesNotContain("DROP TABLE", downgrade);
    }

    [Fact]
    public void Database_provider_overrides_legacy_enabled_and_reloads_monitor_without_overriding_queue_tuning()
    {
        const string prefix = "CONTEXTDEPOT_SESSION_SETTINGS_TEST_";
        Environment.SetEnvironmentVariable(prefix + "AgentSessions__Enabled", "true");
        Environment.SetEnvironmentVariable(prefix + "AgentSessions__MaxAttempts", "4");
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
            Assert.Equal(4, monitor.CurrentValue.MaxAttempts);
            provider.Publish(new DatabaseApplicationSettingsSnapshotBuilder().Build(Records("true")));
            Assert.True(monitor.CurrentValue.Enabled);
            provider.Publish(new DatabaseApplicationSettingsSnapshotBuilder().Build(Records()));
            Assert.False(monitor.CurrentValue.Enabled);
            Assert.Equal(4, monitor.CurrentValue.MaxAttempts);
        }
        finally
        {
            Environment.SetEnvironmentVariable(prefix + "AgentSessions__Enabled", null);
            Environment.SetEnvironmentVariable(prefix + "AgentSessions__MaxAttempts", null);
        }
    }

    private static ContextDepotDbContext CreateDb() => new(
        new DbContextOptionsBuilder<ContextDepotDbContext>().UseNpgsql("Host=localhost;Database=unused").Options,
        new CurrentDepotAccessContext());
}
