using ContextDepot.Infrastructure.Markdown;
using ContextDepot.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Tests;

public sealed class FileSystemMarkdownStoreTests
{
    [Fact]
    public async Task Concurrent_readiness_probes_preserve_existing_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "context-depot-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var existing = Path.Combine(root, ".context-depot-ready");
            await File.WriteAllTextAsync(existing, "keep");
            var environment = new TestHostEnvironment { ContentRootPath = root };
            var store = new FileSystemMarkdownStore(environment,
                Microsoft.Extensions.Options.Options.Create(new MarkdownStoreOptions { MarkdownRoot = root }));

            var probes = await Task.WhenAll(Enumerable.Range(0, 20)
                .Select(_ => Task.Run(store.CanReadAndWrite)));

            Assert.All(probes, Assert.True);
            Assert.Equal("keep", await File.ReadAllTextAsync(existing));
            Assert.Single(Directory.GetFiles(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "ContextDepot.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
