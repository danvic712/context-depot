using ContextDepot.Application.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using System.Data.Common;
using System.Diagnostics;

namespace ContextDepot.MCP.Shared;

internal static class MCPToolErrorMapper
{
    public static void Throw(Exception exception, ILogger logger)
    {
        var correlationId = Activity.Current?.Id ?? Guid.CreateVersion7().ToString("N");
        switch (exception)
        {
            case ContextDepotApplicationException applicationException:
                logger.LogWarning("MCP request failed with application error {ErrorCode}; correlation_id={CorrelationId}", applicationException.ErrorCode, correlationId);
                throw CreateMCPException(applicationException.ErrorCode, applicationException.Message, correlationId);
            case IOException or UnauthorizedAccessException:
                logger.LogError(exception, "MCP request failed because the Markdown root is unavailable; correlation_id={CorrelationId}", correlationId);
                throw CreateMCPException(ApplicationErrorCodes.MarkdownRootUnavailable, correlationId);
            case DbException or DbUpdateException:
                logger.LogError(exception, "MCP request failed because PostgreSQL is unavailable; correlation_id={CorrelationId}", correlationId);
                throw CreateMCPException(ApplicationErrorCodes.DatabaseUnavailable, correlationId);
            default:
                logger.LogError(exception, "MCP request failed with an unexpected error; correlation_id={CorrelationId}", correlationId);
                throw CreateMCPException(ApplicationErrorCodes.InternalError, correlationId);
        }
    }

    private static McpException CreateMCPException(string errorCode, string correlationId) =>
        CreateMCPException(errorCode, ApplicationErrorMessages.Get(errorCode), correlationId);

    private static McpException CreateMCPException(string errorCode, string message, string correlationId) =>
        new($"{errorCode}: {message} (correlation_id={correlationId})");
}
