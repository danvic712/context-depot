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

    [Fact]
    public async Task Delete_is_idempotent_and_scoped_to_workspace_and_validated_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "context-depot-delete-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileSystemMarkdownStore(new TestHostEnvironment { ContentRootPath = root },
                Microsoft.Extensions.Options.Options.Create(new MarkdownStoreOptions { MarkdownRoot = root }));
            var workspacePath = $"workspaces/{Guid.CreateVersion7():N}/old.md";
            var otherPath = $"workspaces/{Guid.CreateVersion7():N}/old.md";
            await store.WriteAtomicAsync(workspacePath, "old", default);
            await store.WriteAtomicAsync(otherPath, "keep", default);
            await Assert.ThrowsAsync<ContextDepot.Application.Shared.Exceptions.ContextDepotApplicationException>(() =>
                store.DeleteAsync("../notes/old.md", default));
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => store.DeleteAsync(workspacePath, canceled.Token));
            Assert.NotNull(await store.GetAsync(workspacePath, default));
            await store.DeleteAsync(workspacePath, default);
            await store.DeleteAsync(workspacePath, default);
            Assert.Null(await store.GetAsync(workspacePath, default));
            Assert.Equal("keep", (await store.GetAsync(otherPath, default))!.Content);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
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
