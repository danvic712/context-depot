namespace ContextDepot.Application.Documents;

public sealed class DocumentWriteCoordinator
{
    private readonly Dictionary<string, DocumentWriteLockEntry> locks = new(StringComparer.Ordinal);
    private readonly object gate = new();

    public async ValueTask<IAsyncDisposable> AcquireAsync(string key, CancellationToken cancellationToken)
    {
        DocumentWriteLockEntry entry;
        lock (gate)
        {
            if (!locks.TryGetValue(key, out entry!))
            {
                entry = new DocumentWriteLockEntry();
                locks.Add(key, entry);
            }

            entry.AddReference();
        }

        try
        {
            await entry.WaitAsync(cancellationToken);
            return new DocumentWriteLease(this, key, entry);
        }
        catch
        {
            ReleaseReference(key, entry);
            throw;
        }
    }

    internal void Release(string key, DocumentWriteLockEntry entry)
    {
        entry.Release();
        ReleaseReference(key, entry);
    }

    private void ReleaseReference(string key, DocumentWriteLockEntry entry)
    {
        lock (gate)
        {
            if (!entry.RemoveReference() || !locks.TryGetValue(key, out var current) || !ReferenceEquals(current, entry))
            {
                return;
            }

            locks.Remove(key);
            entry.Dispose();
        }
    }
}
