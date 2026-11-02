using ContextDepot.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Infrastructure.Jobs;

public sealed class BackgroundJobExecutionGate(ILogger logger) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            await action(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            logger.LogWarning("Background job failed ({ErrorCode}); the next scheduled cycle will retry.",
                InfrastructureErrorCodes.BackgroundJobExecutionFailed);
            throw new InvalidOperationException(InfrastructureErrorCodes.BackgroundJobExecutionFailed);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();
}
