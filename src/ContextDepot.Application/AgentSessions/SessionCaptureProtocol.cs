using System.Security.Cryptography;
using System.Text;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Application.AgentSessions;

/// <summary>Maps versioned client input to the same bounded visible-message protocol.</summary>
public static class SessionCaptureProtocol
{
    public static NormalizedSessionCapture Normalize(SessionCaptureRequest request)
    {
        if (!ValidAgent(request.Agent) || !ValidId(request.EventId) ||
            string.IsNullOrWhiteSpace(request.Workspace) || request.Workspace.Length > 500)
            throw Invalid();

        if (request.SchemaVersion == 1)
        {
            if (request.Mode is not null || request.Messages is not null ||
                !ValidId(request.ClientSessionId) || !ValidId(request.TurnId) ||
                request.Phase is not ("user" or "assistant") || !ValidContent(request.Content))
                throw Invalid();
            return new(request.ClientSessionId!, request.TurnId!,
                [new(request.Phase, request.Content)], [request.EventId]);
        }

        if (request.SchemaVersion != 2 || request.Phase is not null || request.Content is not null ||
            request.Messages is null || request.Messages.Count is < 1 or > 2 || request.Messages.Any(message => message is null ||
                message.Role is not ("user" or "assistant") || !ValidContent(message.Content)))
            throw Invalid();

        if (request.Mode == "message")
        {
            if (request.Messages.Count != 1 || !ValidId(request.ClientSessionId) || !ValidId(request.TurnId))
                throw Invalid();
            return new(request.ClientSessionId!, request.TurnId!, request.Messages, [request.EventId]);
        }

        if (request.Mode != "turn" || request.Messages.Count != 2 ||
            request.Messages[0].Role != "user" || request.Messages[1].Role != "assistant" ||
            request.Messages.Any(message => string.IsNullOrWhiteSpace(message.Content)))
            throw Invalid();

        var standalone = request.ClientSessionId is null && request.TurnId is null;
        if (!standalone && (!ValidId(request.ClientSessionId) || !ValidId(request.TurnId)))
            throw Invalid();
        var identity = $"capture:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.EventId)))}";
        return new(standalone ? identity : request.ClientSessionId!, standalone ? identity : request.TurnId!,
            request.Messages, [$"{identity}:user", $"{identity}:assistant"]);
    }

    public static bool ValidAgent(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 30 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    public static bool ValidId(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 200 &&
        !value.Any(char.IsControl);

    private static bool ValidContent(string? value) => value is null || value.Length <= SessionCaptureOptions.MaxContentLength;

    private static ContextDepotApplicationException Invalid() => new(ApplicationErrorCodes.InvalidSessionCapture);
}
