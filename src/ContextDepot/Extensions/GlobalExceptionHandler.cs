using ContextDepot.Application.Shared.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ContextDepot.Extensions;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
            return false;
        }

        var applicationException = exception as ContextDepotApplicationException;
        var validationException = exception as ValidationException;
        var status = applicationException is null && validationException is null
            ? StatusCodes.Status500InternalServerError
            : StatusCodes.Status400BadRequest;
        var code = applicationException?.ErrorCode
                   ?? validationException?.Errors.FirstOrDefault()?.ErrorCode
                   ?? ApplicationErrorCodes.InternalError;
        var locale = httpContext.Request.Headers.AcceptLanguage.ToString()
            .Split(',', 2)[0].Split(';', 2)[0].Trim();

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "HTTP request failed. TraceId: {TraceId}", httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogWarning("HTTP request failed with error code {ErrorCode}. TraceId: {TraceId}",
                code, httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = ApplicationErrorMessages.Get(code, locale),
                Extensions =
                {
                    ["code"] = code,
                    ["traceId"] = httpContext.TraceIdentifier
                }
            }
        });
    }
}
