using ContextDepot.Domain.AgentSessions;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class SessionCaptureTurnRouteConfiguration : IEntityTypeConfiguration<SessionCaptureTurnRoute>
{
    public void Configure(EntityTypeBuilder<SessionCaptureTurnRoute> entity)
    {
        entity.ToTable("session_capture_turn_routes", table =>
        {
            table.HasCheckConstraint("ck_capture_route_hash", "routing_vector_hash IS NULL OR routing_vector_hash ~ '^[0-9A-F]{64}$'");
            table.HasCheckConstraint("ck_capture_route_state", "state IN ('waiting_user','pending','resolving','resolved','promoted','skipped','failed')");
            table.HasCheckConstraint("ck_capture_route_workspace", "state NOT IN ('resolved','promoted') OR workspace_id IS NOT NULL");
            table.HasCheckConstraint("ck_capture_route_lease", "(state = 'resolving' AND lease_token IS NOT NULL AND lease_until IS NOT NULL) OR (state <> 'resolving' AND lease_token IS NULL AND lease_until IS NULL)");
            table.HasCheckConstraint("ck_capture_route_expiry", "expires_at > created_at");
            table.HasCheckConstraint("ck_capture_route_counts", "attempt_count >= 0 AND provider_call_count >= 0");
            table.HasCheckConstraint("ck_capture_route_vector", "(encrypted_routing_vector IS NULL AND routing_vector_hash IS NULL AND routing_embedding_fingerprint IS NULL) OR (encrypted_routing_vector IS NOT NULL AND routing_vector_hash IS NOT NULL AND routing_embedding_fingerprint IS NOT NULL)");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.DepotId).HasColumnName("depot_id");
        entity.Property(x => x.AccessKeyId).HasColumnName("access_key_id");
        entity.Property(x => x.Agent).HasColumnName("agent").HasMaxLength(30);
        entity.Property(x => x.ClientSessionId).HasColumnName("client_session_id").HasMaxLength(200);
        entity.Property(x => x.TurnId).HasColumnName("turn_id").HasMaxLength(200);
        entity.Property(x => x.WorkspaceId).HasColumnName("workspace_id");
        entity.Property(x => x.State).HasColumnName("state").HasMaxLength(20);
        entity.Property(x => x.ResultCode).HasColumnName("result_code").HasMaxLength(60);
        entity.Property(x => x.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(60);
        entity.Property(x => x.ResolverVersion).HasColumnName("resolver_version").HasMaxLength(80);
        entity.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        entity.Property(x => x.ProviderCallCount).HasColumnName("provider_call_count");
        entity.Property(x => x.LastProviderCallAt).HasColumnName("last_provider_call_at");
        entity.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        entity.Property(x => x.LeaseToken).HasColumnName("lease_token");
        entity.Property(x => x.LeaseUntil).HasColumnName("lease_until");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        entity.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
        entity.Property(x => x.PromotedAt).HasColumnName("promoted_at");
        entity.Property(x => x.LastAttemptAt).HasColumnName("last_attempt_at");
        entity.Property(x => x.EncryptedRoutingVector).HasColumnName("encrypted_routing_vector");
        entity.Property(x => x.RoutingVectorHash).HasColumnName("routing_vector_hash").HasMaxLength(64);
        entity.Property(x => x.RoutingEmbeddingFingerprint).HasColumnName("routing_embedding_fingerprint").HasMaxLength(200);

        entity.HasAlternateKey(x => new { x.Id, x.DepotId, x.AccessKeyId, x.Agent, x.ClientSessionId, x.TurnId });
        entity.HasIndex(x => new { x.DepotId, x.AccessKeyId, x.Agent, x.ClientSessionId, x.TurnId }).IsUnique();
        entity.HasOne<Workspace>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.DepotId }).HasPrincipalKey(x => new { x.Id, x.DepotId }).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => new { x.State, x.NextAttemptAt, x.Id });
        entity.HasIndex(x => new { x.State, x.LeaseUntil, x.Id });
        entity.HasIndex(x => new { x.ExpiresAt, x.Id });
        entity.HasIndex(x => new { x.DepotId, x.AccessKeyId, x.LastProviderCallAt });
        entity.Property(x => x.AttemptCount).HasDefaultValue(0);
        entity.Property(x => x.ProviderCallCount).HasDefaultValue(0);
        entity.HasOne<DepotAccessKey>().WithMany().HasForeignKey(x => new { x.AccessKeyId, x.DepotId }).HasPrincipalKey(x => new { x.Id, x.DepotId }).OnDelete(DeleteBehavior.Restrict);
    }
}
