using ContextDepot.Application.Documents;

namespace ContextDepot.Application.Tests.Documents;

public sealed class DocumentWriteCoordinatorTests
{
    [Fact]
    public async Task Concurrent_acquisitions_can_release_and_reuse_the_same_key()
    {
        var coordinator = new DocumentWriteCoordinator();
        var first = await coordinator.AcquireAsync("workspace:document.md", CancellationToken.None);
        var secondTask = coordinator.AcquireAsync("workspace:document.md", CancellationToken.None).AsTask();

        await Task.Yield();
        await first.DisposeAsync();

        var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(1));
        await second.DisposeAsync();

        var third = await coordinator.AcquireAsync("workspace:document.md", CancellationToken.None);
        await third.DisposeAsync();
    }
}
