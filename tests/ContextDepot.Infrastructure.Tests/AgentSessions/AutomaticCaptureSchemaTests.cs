using ContextDepot.Domain.AgentSessions;
using ContextDepot.Domain.Contexts;
using ContextDepot.Infrastructure.CurrentDepot;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ContextDepot.Infrastructure.Tests.AgentSessions;

public sealed class AutomaticCaptureSchemaTests
{
    private static ContextDepotDbContext CreateDb() => new(new DbContextOptionsBuilder<ContextDepotDbContext>()
        .UseNpgsql("Host=localhost;Database=model_only;Username=model_only").Options, new CurrentDepotAccessContext());

    [Fact]
    public void Inbox_foreign_key_covers_the_entire_route_identity_and_does_not_bind_a_session()
    {
        using var db = CreateDb();
        var inbox = db.Model.FindEntityType(typeof(SessionCaptureInboxEvent))!;
        var fk = Assert.Single(inbox.GetForeignKeys());
        Assert.Equal(new[] { "TurnRouteId", "DepotId", "AccessKeyId", "Agent", "ClientSessionId", "TurnId" }, fk.Properties.Select(p => p.Name));
        Assert.Equal(typeof(SessionCaptureTurnRoute), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.Contains(inbox.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TurnRouteId", "Phase" }));
        Assert.NotEmpty(inbox.GetDeclaredQueryFilters());
    }

    [Fact]
    public void Evidence_is_context_scoped_and_only_context_physical_deletion_cascades()
    {
        using var db = CreateDb();
        var evidence = db.Model.FindEntityType(typeof(ContextCaptureEvidence))!;
        Assert.Equal("ContextId", Assert.Single(evidence.FindPrimaryKey()!.Properties).Name);
        var context = Assert.Single(evidence.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(ContextItem));
        Assert.Equal(new[] { "ContextId", "DepotId", "WorkspaceId" }, context.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.Cascade, context.DeleteBehavior);
        Assert.All(evidence.GetForeignKeys().Where(x => x != context), x => Assert.Equal(DeleteBehavior.Restrict, x.DeleteBehavior));
        Assert.DoesNotContain("CurrentAccessKeyId", Assert.Single(evidence.GetDeclaredQueryFilters()).Expression!.ToString());
        Assert.Contains("AccessibleWorkspaceIds", Assert.Single(evidence.GetDeclaredQueryFilters()).Expression!.ToString());
    }

    [Fact]
    public void Migration_contains_three_tables_usage_backfill_and_preserves_legacy_leases()
    {
        using var db = CreateDb();
        var script = db.GetService<IMigrator>().GenerateScript();
        // Use the complete script to avoid coupling this test to the preceding migration timestamp.
        Assert.Contains("CREATE TABLE public.session_capture_turn_routes", script);
        Assert.Contains("CREATE TABLE public.session_capture_inbox_events", script);
        Assert.Contains("CREATE TABLE public.context_capture_evidence", script);
        Assert.Contains("provider_call_count = attempt_count", script);
        Assert.Contains("CURRENT_TIMESTAMP", script);
        Assert.DoesNotContain("SET lease_token", script);
    }

    [Fact]
    public void Model_includes_token_plan_and_nonnegative_counter_constraints()
    {
        using var db = CreateDb();
        var model = db.GetService<IDesignTimeModel>().Model;
        var events = model.FindEntityType(typeof(SessionCaptureEvent))!;
        Assert.Equal("jsonb", events.FindProperty("CandidateOutcomesJson")!.GetColumnType());
        Assert.Contains(events.GetCheckConstraints(), x => x.Name == "ck_capture_event_user_fields");
        Assert.Contains(events.GetCheckConstraints(), x => x.Name == "ck_capture_event_plan");
        var route = model.FindEntityType(typeof(SessionCaptureTurnRoute))!;
        Assert.Contains(route.GetCheckConstraints(), x => x.Name == "ck_capture_route_lease");
        Assert.Contains(route.GetCheckConstraints(), x => x.Name == "ck_capture_route_counts");
    }
}
