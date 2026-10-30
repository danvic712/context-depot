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
