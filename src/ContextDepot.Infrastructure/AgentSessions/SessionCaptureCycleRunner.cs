using System.ClientModel;
using System.Net;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Enums;
using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ContextDepot.Infrastructure.AgentSessions;

public sealed class SessionCaptureCycleRunner(IServiceScopeFactory scopes,
    IOptions<SessionCaptureOptions> options, ILogger<SessionCaptureCycleRunner> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var queueScope = scopes.CreateAsyncScope();
        queueScope.ServiceProvider.GetRequiredService<CurrentDepotAccessContext>().AllowInternalAccess();
        var queue = queueScope.ServiceProvider.GetRequiredService<ISessionCaptureRepository>();
        await queue.CleanupAsync(cancellationToken);
        if (!options.Value.Enabled) return;
        var turn = await queue.ClaimAsync(cancellationToken);
        if (turn is null) return;
        try
        {
            await using var processingScope = scopes.CreateAsyncScope();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.ExtractionTimeoutSeconds));
            var result = await processingScope.ServiceProvider.GetRequiredService<MemoryCaptureProcessor>()
                .ProcessAsync(turn, timeout.Token);
            await queue.FinishAsync(turn, result, SessionCaptureDisposition.Completed, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ContextDepotApplicationException exception) when (exception.ErrorCode == ApplicationErrorCodes.WorkspaceUnavailable)
        {
            await queue.FinishAsync(turn, "skipped_unauthorized", SessionCaptureDisposition.Completed, cancellationToken);
        }
        catch (Exception exception)
        {
            var disposition = ShouldRetry(exception) ? SessionCaptureDisposition.Retry : SessionCaptureDisposition.Failed;
            // Never log exception text: provider responses can echo captured plaintext or credentials.
            logger.LogWarning("Session capture processing failed for receipt {ReceiptId}; disposition={Disposition}.", turn.UserEventId, disposition);
            var code = exception switch
            {
                InvalidOperationException { Message: InfrastructureErrorCodes.SessionMemoryChatUnavailable } => "chat_unavailable",
                System.Text.Json.JsonException => "invalid_extraction_response",
                ContextDepotApplicationException { ErrorCode: ApplicationErrorCodes.ContextCreateOnlyCapacity } => "capacity_deferred",
                _ => "extraction_failed"
            };
            await queue.FinishAsync(turn, code, disposition, cancellationToken);
        }
    }

    private static bool ShouldRetry(Exception exception) => exception switch
    {
        OperationCanceledException => true,
        HttpRequestException httpException => httpException.StatusCode is null ||
            IsTransientStatus((int)httpException.StatusCode.Value),
        ClientResultException providerException => IsTransientStatus(providerException.Status),
        NpgsqlException databaseException => databaseException.IsTransient,
        DbUpdateException { InnerException: NpgsqlException databaseException } => databaseException.IsTransient,
        InvalidOperationException { Message: InfrastructureErrorCodes.SessionMemoryChatUnavailable } => true,
        ContextDepotApplicationException { ErrorCode: ApplicationErrorCodes.ContextCreateOnlyCapacity } => true,
        _ => false
    };

    private static bool IsTransientStatus(int status) =>
        status is (int)HttpStatusCode.RequestTimeout or (int)HttpStatusCode.TooManyRequests || status >= 500;

}
