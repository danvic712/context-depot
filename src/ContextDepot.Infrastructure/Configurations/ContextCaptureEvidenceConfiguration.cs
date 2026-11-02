using ContextDepot.Domain.AgentSessions;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class ContextCaptureEvidenceConfiguration : IEntityTypeConfiguration<ContextCaptureEvidence>
{
    public void Configure(EntityTypeBuilder<ContextCaptureEvidence> entity)
    {
        entity.ToTable("context_capture_evidence", table =>
        {
            table.HasCheckConstraint("ck_capture_evidence_hashes", "user_content_hash ~ '^[0-9A-F]{64}$' AND fragments_hash ~ '^[0-9A-F]{64}$'");
            table.HasCheckConstraint("ck_capture_evidence_index", "candidate_index BETWEEN 0 AND 2");
            table.HasCheckConstraint("ck_capture_evidence_protection", "protection_version >= 0");
        });
        entity.HasKey(x => x.ContextId);
        entity.Property(x => x.ContextId).HasColumnName("context_id");
        entity.Property(x => x.DepotId).HasColumnName("depot_id");
        entity.Property(x => x.WorkspaceId).HasColumnName("workspace_id");
        entity.Property(x => x.AccessKeyId).HasColumnName("access_key_id");
        entity.Property(x => x.UserEventId).HasColumnName("user_event_id");
        entity.Property(x => x.AssistantEventId).HasColumnName("assistant_event_id");
        entity.Property(x => x.CandidateIndex).HasColumnName("candidate_index");
        entity.Property(x => x.UserContentHash).HasColumnName("user_content_hash").HasMaxLength(64);
        entity.Property(x => x.EncryptedFragments).HasColumnName("encrypted_fragments");
        entity.Property(x => x.FragmentsHash).HasColumnName("fragments_hash").HasMaxLength(64);
        entity.Property(x => x.ExtractorVersion).HasColumnName("extractor_version").HasMaxLength(80);
        entity.Property(x => x.ProtectionVersion).HasColumnName("protection_version");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");

        entity.HasOne<ContextItem>().WithMany().HasForeignKey(x => new { x.ContextId, x.DepotId, x.WorkspaceId }).HasPrincipalKey(x => new { x.Id, x.DepotId, x.WorkspaceId }).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(x => new { x.DepotId, x.AccessKeyId, x.UserEventId, x.CandidateIndex }).IsUnique();
        entity.Property(x => x.ProtectionVersion).HasDefaultValue(1);
        entity.HasOne<DepotAccessKey>().WithMany().HasForeignKey(x => new { x.AccessKeyId, x.DepotId }).HasPrincipalKey(x => new { x.Id, x.DepotId }).OnDelete(DeleteBehavior.Restrict);
    }
}
