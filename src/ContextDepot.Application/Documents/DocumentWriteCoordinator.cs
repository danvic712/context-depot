using System.Collections.Concurrent;

namespace ContextDepot.Application.Documents;

public sealed class DocumentWriteCoordinator
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new(StringComparer.Ordinal);

    public async ValueTask<IAsyncDisposable> AcquireAsync(string key, CancellationToken cancellationToken)
    {
        var semaphore = locks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(key, semaphore, locks);
    }

    private sealed class Releaser(string key, SemaphoreSlim semaphore, ConcurrentDictionary<string, SemaphoreSlim> locks) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            if (semaphore.CurrentCount == 1)
            {
                locks.TryRemove(new KeyValuePair<string, SemaphoreSlim>(key, semaphore));
            }

            semaphore.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
