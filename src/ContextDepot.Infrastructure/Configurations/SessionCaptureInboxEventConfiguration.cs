using ContextDepot.Domain.AgentSessions;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class SessionCaptureInboxEventConfiguration : IEntityTypeConfiguration<SessionCaptureInboxEvent>
{
    public void Configure(EntityTypeBuilder<SessionCaptureInboxEvent> entity)
    {
        entity.ToTable("session_capture_inbox_events", table =>
        {
            table.HasCheckConstraint("ck_capture_inbox_hash", "content_hash ~ '^[0-9A-F]{64}$'");
            table.HasCheckConstraint("ck_capture_inbox_phase", "phase IN ('user','assistant')");
            table.HasCheckConstraint("ck_capture_inbox_content", "(encrypted_content IS NULL) = (content_purged_at IS NOT NULL)");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.TurnRouteId).HasColumnName("turn_route_id");
        entity.Property(x => x.DepotId).HasColumnName("depot_id");
        entity.Property(x => x.AccessKeyId).HasColumnName("access_key_id");
        entity.Property(x => x.Agent).HasColumnName("agent").HasMaxLength(30);
        entity.Property(x => x.ClientSessionId).HasColumnName("client_session_id").HasMaxLength(200);
        entity.Property(x => x.TurnId).HasColumnName("turn_id").HasMaxLength(200);
        entity.Property(x => x.EventId).HasColumnName("event_id").HasMaxLength(200);
        entity.Property(x => x.Phase).HasColumnName("phase").HasMaxLength(20);
        entity.Property(x => x.EncryptedContent).HasColumnName("encrypted_content");
        entity.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(64);
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.ContentPurgedAt).HasColumnName("content_purged_at");

        entity.HasOne<SessionCaptureTurnRoute>().WithMany().HasForeignKey(x => new { x.TurnRouteId, x.DepotId, x.AccessKeyId, x.Agent, x.ClientSessionId, x.TurnId }).HasPrincipalKey(x => new { x.Id, x.DepotId, x.AccessKeyId, x.Agent, x.ClientSessionId, x.TurnId }).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => new { x.DepotId, x.AccessKeyId, x.Agent, x.ClientSessionId, x.EventId }).IsUnique();
        entity.HasIndex(x => new { x.TurnRouteId, x.Phase }).IsUnique();
        entity.HasIndex(x => x.TurnRouteId);
    }
}
