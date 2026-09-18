using ContextDepot.Application.Abstractions;
using ModelContextProtocol;

namespace ContextDepot.Mcp;

internal static class McpToolErrorMapper
{
    public static void Throw(Exception exception)
    {
        if (exception is ContextDepotApplicationException applicationException)
        {
            throw new McpException($"{applicationException.ErrorCode}: {applicationException.Message}");
        }

        throw new McpException("InternalError: ContextDepot could not complete the request.");
    }
}
