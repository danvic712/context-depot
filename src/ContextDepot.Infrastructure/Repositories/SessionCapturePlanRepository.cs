using System.Text.Json;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Infrastructure.Exceptions;
using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Contexts.Enums;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Domain.AgentSessions;
using ContextDepot.Domain.Contexts;
using ContextDepot.Infrastructure.AgentSessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class SessionCapturePlanRepository(ContextDepotDbContext db, ISessionContentProtector contentProtector,
    ICaptureEvidenceProtector evidenceProtector, IOptionsMonitor<SessionCaptureOptions> options, ISecretDetector secrets) : ISessionCapturePlanRepository
{
    public async Task<LeaseCommitResult> StorePlanAsync(CapturedTurn turn, ValidatedExtractionPlan plan, CancellationToken cancellationToken)
    {
        var plaintext = CapturePlanCodec.Serialize(plan);
        var revalidated = new ExtractionPlanValidator(secrets).Validate(plan.Candidates.Select(x =>
            new MemoryCandidate(x.Kind, x.Key, Summary: x.Summary, Quotes: x.Evidence.Select(e => e.Text).ToArray())).ToArray(), turn.UserContent);
        if (CapturePlanCodec.Serialize(revalidated) != plaintext) return new("conflict");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockAndValidateAsync(turn, cancellationToken);
        if (user is null) return new("lease_lost");
        if (user.EncryptedExtractionPlan is not null)
        {
            var existing = CapturePlanCodec.Read(contentProtector.Unprotect(user.EncryptedExtractionPlan), user.ExtractionPlanHash!);
            return new(CapturePlanCodec.Serialize(existing) == plaintext ? "committed" : "conflict");
        }
        user.EncryptedExtractionPlan = contentProtector.Protect(plaintext);
        user.ExtractionPlanHash = CapturePlanCodec.Hash(plaintext);
        user.ExtractorVersion = plan.Version;
        user.PersistenceAttemptCount = 1;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new("committed");
    }

    public async Task<CaptureCandidateOutcome> SaveCandidateAsync(CapturedTurn turn, int index, PreparedCaptureContext prepared,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var user = await LockAndValidateAsync(turn, cancellationToken) ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionCaptureLeaseLost);
            var plan = ReadPlan(user);
            var candidate = plan.Candidates.SingleOrDefault(x => x.Index == index) ?? throw new InvalidOperationException(InfrastructureErrorCodes.SessionMemoryContentUnavailable);
            var outcomes = ReadOutcomes(user);
            var prior = outcomes.SingleOrDefault(x => x.Index == index);
            if (prior is not null) return prior;
            var item = prepared.Context;
            // Prepared values cannot change the immutable plan or its authorization boundary.
            if (item.DepotId != turn.DepotId || item.WorkspaceId != turn.WorkspaceId || item.Key != candidate.Key ||
                !string.Equals(item.Kind.ToString(), candidate.Kind, StringComparison.OrdinalIgnoreCase) ||
                item.Content != string.Join(' ', candidate.Summary.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) ||
                item.SourceType != ContextDepot.Domain.Contexts.Enums.SourceType.Agent ||
                item.VerificationStatus != ContextDepot.Domain.Contexts.Enums.VerificationStatus.Unknown ||
                item.ProvenanceTrust != ContextDepot.Domain.Contexts.Enums.ProvenanceTrust.Asserted ||
                item.SourceAgent != turn.Agent || item.SourceRef != $"session-capture:{turn.UserEventId}:{turn.AssistantEventId}")
                throw new JsonException(InfrastructureErrorCodes.SessionMemoryResponseInvalid);
            var evidence = await db.ContextCaptureEvidence.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x =>
                x.DepotId == turn.DepotId && x.AccessKeyId == turn.AccessKeyId && x.UserEventId == turn.UserEventId &&
                x.CandidateIndex == index, cancellationToken);
            CaptureCandidateOutcome outcome;
            if (evidence is not null)
            {
                if (evidence.WorkspaceId != turn.WorkspaceId || evidence.UserContentHash != user.ContentHash)
                    throw new InvalidOperationException(InfrastructureErrorCodes.SessionMemoryContentUnavailable);
                outcome = new(index, "created", evidence.ContextId);
            }
            else
            {
                var result = await new ContextRepository(db).CompareAndStageCreateOnlyAsync(item, cancellationToken);
                outcome = new(index, result.Outcome == ContextPersistenceOutcome.Created ? "created" : "reused", result.Context!.Id);
                if (outcome.Outcome == "created")
                {
                    var fragments = JsonSerializer.Serialize(candidate.Evidence, CapturePlanCodec.Json);
                    db.ContextCaptureEvidence.Add(new ContextCaptureEvidence
                    {
                        ContextId = item.Id, DepotId = turn.DepotId, WorkspaceId = turn.WorkspaceId, AccessKeyId = turn.AccessKeyId,
                        UserEventId = turn.UserEventId, AssistantEventId = turn.AssistantEventId, CandidateIndex = index,
                        UserContentHash = user.ContentHash, EncryptedFragments = evidenceProtector.Protect(fragments),
                        FragmentsHash = CapturePlanCodec.Hash(fragments), ExtractorVersion = plan.Version,
                        ProtectionVersion = 1, CreatedAt = item.CreatedAt
                    });
                }
            }
            if (!await IsCurrentAsync(turn, cancellationToken))
                throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionCaptureLeaseLost);
            outcomes.Add(outcome);
            user.CandidateOutcomesJson = JsonSerializer.Serialize(outcomes.OrderBy(x => x.Index), CapturePlanCodec.Json);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return outcome;
        }
        catch
        {
            // The same scoped DbContext is used by FinishAsync after a persistence failure.
            // Rolled-back inserts must never leak into its later SaveChanges call.
            foreach (var entry in db.ChangeTracker.Entries().Where(x =>
                         (x.Entity is ContextItem or ContextCaptureEvidence) && x.State == EntityState.Added).ToArray())
                entry.State = EntityState.Detached;
            throw;
        }
    }

    public async Task<LeaseCommitResult> RejectCandidateAsync(CapturedTurn turn, int index, string reason, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockAndValidateAsync(turn, cancellationToken);
        if (user is null) return new("lease_lost");
        if (!ReadPlan(user).Candidates.Any(x => x.Index == index)) return new("conflict");
        var outcomes = ReadOutcomes(user);
        if (outcomes.All(x => x.Index != index))
        {
            outcomes.Add(new(index, "rejected"));
            user.CandidateOutcomesJson = JsonSerializer.Serialize(outcomes.OrderBy(x => x.Index), CapturePlanCodec.Json);
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new("committed");
    }

    private ValidatedExtractionPlan ReadPlan(SessionCaptureEvent user) =>
        CapturePlanCodec.Read(contentProtector.Unprotect(user.EncryptedExtractionPlan ?? throw new InvalidOperationException(InfrastructureErrorCodes.SessionMemoryContentUnavailable)),
            user.ExtractionPlanHash ?? throw new InvalidOperationException(InfrastructureErrorCodes.SessionMemoryContentUnavailable));

    private static List<CaptureCandidateOutcome> ReadOutcomes(SessionCaptureEvent user) =>
        JsonSerializer.Deserialize<List<CaptureCandidateOutcome>>(user.CandidateOutcomesJson, CapturePlanCodec.Json) ?? [];

    private async Task<SessionCaptureEvent?> LockAndValidateAsync(CapturedTurn turn, CancellationToken cancellationToken)
    {
        await SessionCaptureLocks.LockKeyAsync(db, turn.DepotId, turn.AccessKeyId, cancellationToken);
        var rows = await SessionCaptureLocks.LockTurnAsync(db, turn.UserEventId, turn.AssistantEventId, cancellationToken);
        foreach (var row in rows) await db.Entry(row).ReloadAsync(cancellationToken);
        var now = await db.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellationToken);
        if (!options.CurrentValue.Enabled || turn.LeaseToken is null || rows.Length != 2 ||
            rows.Any(x => x.DepotId != turn.DepotId || x.AccessKeyId != turn.AccessKeyId || x.WorkspaceId != turn.WorkspaceId ||
                x.Status != "processing" || x.LeaseToken != turn.LeaseToken || x.LeaseUntil <= now || x.LeaseUntil is null ||
                x.EncryptedContent is null || (x.CaptureExpiresAt ?? x.CreatedAt.AddHours(options.CurrentValue.RetentionHours)) <= now)) return null;
        var user = rows.SingleOrDefault(x => x.Id == turn.UserEventId && x.Phase == "user");
        var assistant = rows.SingleOrDefault(x => x.Id == turn.AssistantEventId && x.Phase == "assistant");
        if (user is null || assistant is null || user.SessionId != assistant.SessionId || user.Agent != assistant.Agent ||
            user.ClientSessionId != assistant.ClientSessionId || user.TurnId != assistant.TurnId ||
            CapturePlanCodec.Hash(turn.UserContent) != user.ContentHash || CapturePlanCodec.Hash(turn.AssistantContent) != assistant.ContentHash) return null;
        await SessionAccessGuard.EnsureAsync(db, turn.DepotId, turn.AccessKeyId, turn.WorkspaceId, cancellationToken);
        return await IsCurrentAsync(turn, cancellationToken) ? user : null;
    }

    private async Task<bool> IsCurrentAsync(CapturedTurn turn, CancellationToken cancellationToken)
    {
        var now = await SessionCaptureLocks.NowAsync(db, cancellationToken);
        var rows = db.SessionCaptureEvents.Local.Where(x => x.Id == turn.UserEventId || x.Id == turn.AssistantEventId).ToArray();
        return options.CurrentValue.Enabled && turn.LeaseToken is not null && rows.Length == 2 && rows.All(x =>
            x.Status == "processing" && x.LeaseToken == turn.LeaseToken && x.LeaseUntil > now &&
            (x.CaptureExpiresAt ?? x.CreatedAt.AddHours(options.CurrentValue.RetentionHours)) > now);
    }
}
