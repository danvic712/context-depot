using ContextDepot.Domain.AgentSessions;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class SessionCaptureEventConfiguration : IEntityTypeConfiguration<SessionCaptureEvent>
{
    public void Configure(EntityTypeBuilder<SessionCaptureEvent> entity)
    {
        entity.ToTable("session_capture_events", table =>
        {
            table.HasCheckConstraint("ck_capture_event_plan_hash", "extraction_plan_hash IS NULL OR extraction_plan_hash ~ '^[0-9A-F]{64}$'");
            table.HasCheckConstraint("ck_capture_event_outcomes", "jsonb_typeof(candidate_outcomes_json) = 'array' AND jsonb_array_length(candidate_outcomes_json) <= 3");
            table.HasCheckConstraint("ck_capture_event_counts", "attempt_count >= 0 AND provider_call_count >= 0 AND persistence_attempt_count >= 0");
            table.HasCheckConstraint("ck_capture_event_plan", "(encrypted_extraction_plan IS NULL) = (extraction_plan_hash IS NULL)");
            table.HasCheckConstraint("ck_capture_event_user_fields", "phase = 'user' OR (encrypted_extraction_plan IS NULL AND extraction_plan_hash IS NULL AND extractor_version IS NULL AND candidate_outcomes_json = '[]'::jsonb AND provider_call_count = 0 AND last_provider_call_at IS NULL AND persistence_attempt_count = 0)");
            table.HasCheckConstraint("ck_session_capture_phase", "phase IN ('user', 'assistant')");
            table.HasCheckConstraint("ck_session_capture_status", "status IN ('pending', 'processing', 'completed', 'failed')");
        });
        entity.HasKey(x => x.Id);
        entity.HasOne<AgentSession>().WithMany().HasForeignKey(x => new { x.SessionId, x.DepotId, x.AccessKeyId, x.WorkspaceId })
            .HasPrincipalKey(s => new { s.Id, s.DepotId, s.AccessKeyId, s.WorkspaceId }).OnDelete(DeleteBehavior.Restrict);
        foreach (var property in entity.Metadata.GetProperties())
        {
            var name = System.Text.RegularExpressions.Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
            entity.Property(property.Name).HasColumnName(name);
        }
        entity.Property(x => x.LastErrorCode).HasMaxLength(60);
        entity.Property(x => x.ExtractionPlanHash).HasMaxLength(64);
        entity.Property(x => x.ExtractorVersion).HasMaxLength(80);
        entity.Property(x => x.ProviderCallCount).HasDefaultValue(0);
        entity.Property(x => x.PersistenceAttemptCount).HasDefaultValue(0);
        entity.Property(x => x.CandidateOutcomesJson).HasColumnType("jsonb").HasDefaultValue("[]");
        entity.HasIndex(x => new { x.DepotId, x.AccessKeyId, x.LastProviderCallAt });
        entity.Property(x => x.Agent).HasMaxLength(30);
        entity.Property(x => x.ClientSessionId).HasMaxLength(200);
        entity.Property(x => x.TurnId).HasMaxLength(200);
        entity.Property(x => x.EventId).HasMaxLength(200);
        entity.Property(x => x.Phase).HasMaxLength(20);
        entity.Property(x => x.Status).HasMaxLength(20);
        entity.Property(x => x.ContentHash).HasMaxLength(64);
        entity.Property(x => x.ResultCode).HasMaxLength(60);
        entity.HasIndex(x => new { x.DepotId, x.AccessKeyId, x.Agent, x.ClientSessionId, x.EventId }).IsUnique()
            .HasDatabaseName("ux_session_capture_event_identity");
        // Also prevents a second event ID from becoming another message in the same turn.
        entity.HasIndex(x => new { x.DepotId, x.AccessKeyId, x.Agent, x.ClientSessionId, x.TurnId, x.Phase }).IsUnique()
            .HasDatabaseName("ux_session_capture_turn_phase");
        entity.HasIndex(x => new { x.Status, x.NextAttemptAt }).HasDatabaseName("ix_session_capture_pending");
        entity.HasOne<Workspace>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.DepotId })
            .HasPrincipalKey(x => new { x.Id, x.DepotId }).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<DepotAccessKey>().WithMany().HasForeignKey(x => new { x.AccessKeyId, x.DepotId })
            .HasPrincipalKey(x => new { x.Id, x.DepotId }).OnDelete(DeleteBehavior.Restrict);
    }
}
