using ContextDepot.Application.AgentContext.Dtos;
using ContextDepot.Application.AgentContext.Enums;
using ContextDepot.Application.Shared.Localization;

namespace ContextDepot.Application.AgentContext;

public static class AgentContextMessages
{
    private static readonly EmbeddedLocaleCatalog Catalog = new(typeof(AgentContextMessages).Assembly);
    private static readonly HashSet<string> Acknowledgements = new(
        Catalog.GetValues("agentAcknowledgements").SelectMany(value => value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
        StringComparer.OrdinalIgnoreCase);

    public static bool IsAcknowledgement(string input) => Acknowledgements.Contains(input);

    public static AgentContextResult AddGuidance(AgentContextResult result, string? locale = null,
        bool automaticCapture = false)
    {
        if (result.Event.Type == AgentContextEventType.OperationCompleted && result.NextActions.Count == 0)
            return result with { Guidance = null };

        var messages = Catalog.GetMessages(locale);
        var notice = result.Event.Type == AgentContextEventType.OperationCompleted ? "agentActivityNotice" :
            result.Retrieval.Status == AgentRetrievalStatus.Skipped ? "agentSkippedNotice" : "agentDataNotice";
        var guidance = messages[notice] + " " + messages["agentWorkflowNotice"];
        if (automaticCapture) guidance += " " + messages["agentAutomaticCaptureNotice"];
        return result with { Guidance = guidance };
    }
}
