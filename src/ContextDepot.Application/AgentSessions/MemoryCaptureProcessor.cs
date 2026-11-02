using System.Text.Json;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.AgentSessions.Enums;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Safety.Contracts;

namespace ContextDepot.Application.AgentSessions;

public sealed class MemoryCaptureProcessor(ISessionCaptureAuthorization authorization,
    IMemoryExtractionService extraction, IContextAppService contexts, ISecretDetector secrets,
    ISessionCapturePlanRepository plans, ISessionCaptureCallBudget budget)
{
    public async Task<CaptureProcessingResult> ProcessAsync(CapturedTurn turn, CancellationToken cancellationToken)
    {
        var workspace = await authorization.AuthorizeAsync(turn, cancellationToken);
        if (workspace is null) return new(SessionCaptureDisposition.Completed, "skipped_unauthorized");
        if (secrets.Detect(turn.UserContent).IsSecret || secrets.Detect(turn.AssistantContent).IsSecret)
            return new(SessionCaptureDisposition.Completed, "skipped_sensitive");
        var plan = turn.Plan;
        if (plan is null)
        {
            if (turn.LeaseToken is not Guid token) return new(SessionCaptureDisposition.Failed, null, "lease_lost");
            var reservation = await budget.TryReserveAsync(new(turn.DepotId, turn.AccessKeyId, "extraction", turn.UserEventId, token),
                ProviderCallKind.ExtractionChat, cancellationToken);
            if (reservation.Status != "granted") return reservation.Status switch
            {
                "expired" => new(SessionCaptureDisposition.Completed, "skipped_expired"),
                "lease_lost" => new(SessionCaptureDisposition.Failed, null, "lease_lost"),
                _ => new(SessionCaptureDisposition.Retry, null, "budget_wait", reservation.WaitUntil)
            };
            try
            {
                var candidates = await extraction.ExtractAsync(turn.UserContent, turn.AssistantContent, cancellationToken);
                plan = new ExtractionPlanValidator(secrets).Validate(candidates, turn.UserContent);
            }
            catch (JsonException) { return new(SessionCaptureDisposition.Failed, "invalid_extraction_response"); }
            var stored = await plans.StorePlanAsync(turn, plan, cancellationToken);
            if (stored.Status != "committed") return new(SessionCaptureDisposition.Failed, null, stored.Status);
            turn = turn with { Plan = plan };
        }
        var created = false;
        foreach (var candidate in plan.Candidates.OrderBy(x => x.Index))
        {
            workspace = await authorization.AuthorizeAsync(turn, cancellationToken);
            if (workspace is null) return new(SessionCaptureDisposition.Failed, created ? "saved" : "skipped_unauthorized", "skipped_unauthorized");
            PreparedCaptureContext prepared;
            try { prepared = contexts.PrepareCaptureContext(turn, candidate, workspace); }
            catch (ContextDepotApplicationException)
            {
                var rejected = await plans.RejectCandidateAsync(turn, candidate.Index, "safety_rejected", cancellationToken);
                if (rejected.Status != "committed") return new(SessionCaptureDisposition.Failed, created ? "saved" : null, rejected.Status);
                continue;
            }
            var outcome = await plans.SaveCandidateAsync(turn, candidate.Index, prepared, cancellationToken);
            created |= outcome.Outcome == "created";
        }
        // Finish re-aggregates durable outcomes, including successes from earlier attempts.
        return new(SessionCaptureDisposition.Completed, created ? "saved" : "skipped_no_new_memory");
    }
}
