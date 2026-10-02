using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Infrastructure.Markdown;
using ContextDepot.Infrastructure.Options;
using ContextDepot.Infrastructure.Repositories;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ContextDepot.Infrastructure.Tests;

public sealed class SettingsOverviewRepositoryTests
{
    [PostgreSqlFact]
    public async Task UnconfiguredEmbeddingDoesNotResolveVectorServicesOrHideStorageStatus()
    {
        var access = new CurrentDepotAccessContext();
        access.AllowInternalAccess();
        var options = new DbContextOptionsBuilder<ContextDepotDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("CONTEXTDEPOT_TEST_CONNECTION")).Options;
        await using var db = new ContextDepotDbContext(options, access);
        var root = Path.Combine(Path.GetTempPath(), "context-depot-settings-" + Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new FileSystemMarkdownStore(new TestEnvironment { ContentRootPath = root },
                Microsoft.Extensions.Options.Options.Create(new MarkdownStoreOptions { MarkdownRoot = root }));
            using var services = new ServiceCollection().BuildServiceProvider();
            var inference = new ScopedInferenceRuntimeSnapshot(new InferenceRuntimeSnapshot(null, InferenceRuntimeState.Unconfigured, null));
            var result = await new SettingsOverviewRepository(db, store, inference, services, TimeProvider.System)
                .GetAsync(Guid.CreateVersion7(), "Test Depot", default);
            Assert.Equal("available", result.DatabaseState);
            Assert.Equal("available", result.MarkdownState);
            Assert.Equal("unconfigured", result.SemanticState);
            Assert.Equal("unconfigured", result.IndexState);
            Assert.Null(result.TotalCount);
            Assert.Equal("/mcp", result.McpPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "ContextDepot.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
