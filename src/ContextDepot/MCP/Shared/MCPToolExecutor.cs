using Microsoft.Extensions.Logging;

namespace ContextDepot.MCP.Shared;

internal static class MCPToolExecutor
{
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, ILogger logger)
    {
        try
        {
            return await operation();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MCPToolErrorMapper.Throw(exception, logger);
            throw;
        }
    }

    public static async Task ExecuteAsync(Func<Task> operation, ILogger logger)
    {
        try
        {
            await operation();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MCPToolErrorMapper.Throw(exception, logger);
            throw;
        }
    }
}
