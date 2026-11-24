using ContextDepot.Domain.AgentSessions;
using ContextDepot.Domain.Access;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class AgentSessionConfiguration : IEntityTypeConfiguration<AgentSession>
{
    public void Configure(EntityTypeBuilder<AgentSession> entity)
    {
        entity.ToTable("agent_sessions", table => table.HasCheckConstraint("ck_agent_session_ended",
            "(ended_at IS NULL AND end_reason IS NULL) OR (ended_at IS NOT NULL AND end_reason IS NOT NULL AND end_reason IN ('completed', 'interrupted', 'other'))"));
        entity.HasKey(s => s.Id);
        foreach (var property in entity.Metadata.GetProperties())
        {
            var name = System.Text.RegularExpressions.Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
            entity.Property(property.Name).HasColumnName(name);
        }
        entity.Property(s => s.Agent).HasMaxLength(30);
        entity.Property(s => s.ClientSessionId).HasMaxLength(200);
        entity.Property(s => s.EndReason).HasMaxLength(20);
        entity.HasIndex(s => new { s.AccessKeyId, s.Agent, s.ClientSessionId }).IsUnique()
            .HasDatabaseName("ux_agent_session_client");
        entity.HasAlternateKey(s => new { s.Id, s.AccessKeyId, s.WorkspaceId });
        entity.HasOne<Workspace>().WithMany().HasForeignKey(s => s.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<AccessKey>().WithMany().HasForeignKey(s => s.AccessKeyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
