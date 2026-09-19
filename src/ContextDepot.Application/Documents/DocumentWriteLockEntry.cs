namespace ContextDepot.Application.Documents;

internal sealed class DocumentWriteLockEntry : IDisposable
{
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private int referenceCount;

    public void AddReference() => referenceCount++;

    public bool RemoveReference() => --referenceCount == 0;

    public Task WaitAsync(CancellationToken cancellationToken) => semaphore.WaitAsync(cancellationToken);

    public void Release() => semaphore.Release();

    public void Dispose() => semaphore.Dispose();
}
