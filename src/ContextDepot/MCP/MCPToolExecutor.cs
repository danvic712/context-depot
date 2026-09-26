namespace ContextDepot.MCP;

public sealed class MCPToolExecutor(IHttpContextAccessor httpContextAccessor)
{
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, ILogger logger)
    {
        try
        {
            return await operation();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MCPToolErrorMapper.Throw(exception, logger, GetRequestLocale());
            throw;
        }
    }

    public async Task ExecuteAsync(Func<Task> operation, ILogger logger)
    {
        try
        {
            await operation();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MCPToolErrorMapper.Throw(exception, logger, GetRequestLocale());
            throw;
        }
    }

    private string? GetRequestLocale()
    {
        var acceptedLanguages = httpContextAccessor.HttpContext?.Request.Headers.AcceptLanguage.ToString();
        return acceptedLanguages?.Split(',', 2)[0].Split(';', 2)[0].Trim();
    }
}