namespace ContextDepot.Application.Documents;

internal sealed class DocumentWriteLease(
    DocumentWriteCoordinator coordinator,
    string key,
    DocumentWriteLockEntry entry) : IAsyncDisposable
{
    private int disposed;

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            coordinator.Release(key, entry);
        }

        return ValueTask.CompletedTask;
    }
}
