using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Exceptions;
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

        var businessException = exception as ContextDepotBusinessException;
        var validationException = exception as ValidationException;
        var code = businessException is not null
            ? KnownCode(businessException.ErrorCode, ApplicationErrorCodes.InternalError)
            : validationException is not null
                ? KnownCode(validationException.Errors.FirstOrDefault()?.ErrorCode, ApplicationErrorCodes.InvalidRequest)
                : ApplicationErrorCodes.InternalError;
        var status = GetStatusCode(code);
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
        if (status == StatusCodes.Status401Unauthorized)
        {
            httpContext.Response.Headers.WWWAuthenticate = "ContextDepotKey";
        }

        var fieldCodes = businessException?.FieldErrorCodes ?? validationException?.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error =>
                KnownCode(error.ErrorCode, ApplicationErrorCodes.InvalidRequest)).ToArray());
        var problem = new ProblemDetails
        {
            Status = status,
            Title = ApplicationErrorMessages.Get(code, locale),
            Extensions =
            {
                ["code"] = code,
                ["traceId"] = httpContext.TraceIdentifier
            }
        };
        if (fieldCodes is not null)
        {
            problem.Extensions["errors"] = fieldCodes.ToDictionary(pair => pair.Key,
                pair => pair.Value.Select(errorCode => ApplicationErrorMessages.Get(
                    KnownCode(errorCode, ApplicationErrorCodes.InvalidRequest), locale)).Distinct().ToArray());
        }

        if (await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem
        })) return true;

        await httpContext.Response.WriteAsJsonAsync(problem, options: null,
            contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }

    private static string KnownCode(string? code, string fallback) =>
        code is not null && ApplicationErrorMessages.Contains(code) ? code : fallback;

    private static int GetStatusCode(string code) => code switch
    {
        ApplicationErrorCodes.Unauthorized => StatusCodes.Status401Unauthorized,
        ApplicationErrorCodes.ContextNotFound or ApplicationErrorCodes.DocumentNotFound or
            ApplicationErrorCodes.WorkspaceNotFound or ApplicationErrorCodes.WorkspaceParentNotFound => StatusCodes.Status404NotFound,
        ApplicationErrorCodes.WorkspacePathConflict or ApplicationErrorCodes.WorkspaceConcurrencyConflict or
            ApplicationErrorCodes.ContextConcurrencyConflict or ApplicationErrorCodes.ContextKindConflict or
            ApplicationErrorCodes.DocumentConflict => StatusCodes.Status409Conflict,
        ApplicationErrorCodes.WebDepotUnavailable or ApplicationErrorCodes.DatabaseUnavailable or
            ApplicationErrorCodes.MarkdownRootUnavailable or ApplicationErrorCodes.EmbeddingGeneratorUnavailable => StatusCodes.Status503ServiceUnavailable,
        ApplicationErrorCodes.InternalError or ApplicationErrorCodes.ContextWriteFailed or
            ApplicationErrorCodes.WorkspaceWriteFailed or ApplicationErrorCodes.DocumentWriteFailed or
            ApplicationErrorCodes.VectorSearchFailed => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status400BadRequest
    };
}
