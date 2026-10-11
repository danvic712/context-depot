using System.Data;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ContextDepot.Infrastructure.Repositories;

public sealed partial class WorkspaceRepository
{
    public async Task<WorkspaceWriteSnapshot> LoadEnsureSnapshotAsync(Guid depotId, WorkspaceSubject? subject,
        CancellationToken cancellationToken)
    {
        WorkspaceAccessSnapshot access;
        if (workspaceAccess.HasUnrestrictedAccess)
            access = new(null, true, true, true, 0, []);
        else
        {
            var key = await db.DepotAccessKeys.IgnoreQueryFilters().AsNoTracking().Include(item => item.WorkspaceGrants)
                .SingleOrDefaultAsync(item => item.DepotId == depotId && item.Id == workspaceAccess.DepotAccessKeyId, cancellationToken);
            access = new(workspaceAccess.DepotAccessKeyId, false, key?.IsActive == true,
                key?.CanCreateRootWorkspaces == true, key?.AccessVersion ?? 0,
                key?.WorkspaceGrants.Select(item => item.WorkspaceId).Order().ToArray() ?? []);
        }
        // Controlled same-depot lookup detects hidden conflicts without exposing their identity.
        var workspaces = await db.Workspaces.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.DepotId == depotId).OrderBy(item => item.Id).ToListAsync(cancellationToken);
        var paths = WorkspacePath.BuildPaths(workspaces);
        var binding = subject is null ? null : await db.WorkspaceSubjectBindings.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(item => item.DepotId == depotId && item.SubjectType == subject.Type && item.SubjectKey == subject.Key,
                cancellationToken);
        return new(subject, binding, workspaces.Select(item => new WorkspacePathLookup(item, paths[item.Id])).ToArray(), access);
    }

    public async Task<WorkspaceWritePersistenceResult> TryApplyEnsureAsync(Guid depotId, WorkspaceWritePlan plan,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            DepotAccessKey? key = null;
            if (plan.Expected.Access.KeyId is Guid keyId)
            {
                key = (await db.DepotAccessKeys.FromSqlInterpolated(
                    $"SELECT * FROM public.depot_access_keys WHERE id = {keyId} AND depot_id = {depotId} FOR UPDATE")
                    .AsNoTracking().ToListAsync(cancellationToken)).SingleOrDefault();
            }
            var current = await LoadEnsureSnapshotAsync(depotId, plan.Expected.Subject, cancellationToken);
            if (!SameSnapshot(plan, current))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                return new(false, current.Access.AccessVersion);
            }

            db.Workspaces.AddRange(plan.Created);
            if (plan.UpdateTarget)
            {
                var target = await db.Workspaces.IgnoreQueryFilters().SingleAsync(
                    item => item.DepotId == depotId && item.Id == plan.Target.Workspace.Id, cancellationToken);
                target.Update(plan.Target.Workspace.Name, plan.Target.Workspace.Description, plan.Target.Workspace.MetadataJson, now);
            }
            if (plan.NewBinding is not null) db.WorkspaceSubjectBindings.Add(plan.NewBinding);
            if (key is not null && plan.Created.Count > 0)
                db.WorkspaceAccessGrants.AddRange(plan.Created.Select(item => new WorkspaceAccessGrant(key.Id, depotId, item.Id, now)));
            await db.SaveChangesAsync(cancellationToken);
            var version = current.Access.AccessVersion;
            if (key is not null && plan.Created.Count > 0)
            {
                version = checked(version + 1);
                await db.DepotAccessKeys.Where(item => item.DepotId == depotId && item.Id == key.Id)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.AccessVersion, version), cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            topologyProvider.Invalidate(depotId);
            return new(true, version);
        }
        catch (Exception error) when (IsWorkspaceCompetition(error))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            return new(false, plan.Expected.Access.AccessVersion);
        }
    }

    private static bool SameSnapshot(WorkspaceWritePlan plan, WorkspaceWriteSnapshot current)
    {
        var expected = plan.Expected;
        var oldTarget = expected.Workspaces.SingleOrDefault(item => item.Workspace.Id == plan.Target.Workspace.Id);
        var newTarget = current.Workspaces.SingleOrDefault(item => item.Workspace.Id == plan.Target.Workspace.Id);
        return
        expected.Access.KeyId == current.Access.KeyId && expected.Access.Unrestricted == current.Access.Unrestricted &&
        expected.Access.IsActive == current.Access.IsActive && expected.Access.CanCreateRootWorkspaces == current.Access.CanCreateRootWorkspaces &&
        expected.Access.AccessVersion == current.Access.AccessVersion && expected.Access.WorkspaceIds.SequenceEqual(current.Access.WorkspaceIds) &&
        expected.Binding?.Id == current.Binding?.Id && expected.Binding?.WorkspaceId == current.Binding?.WorkspaceId &&
        expected.Workspaces.Select(TopologyFingerprint).SequenceEqual(current.Workspaces.Select(TopologyFingerprint)) &&
        (oldTarget is null || newTarget is not null && DetailsFingerprint(oldTarget) == DetailsFingerprint(newTarget));
    }

    private static (Guid Id, Guid? ParentId, string Path) TopologyFingerprint(WorkspacePathLookup item) =>
        (item.Workspace.Id, item.Workspace.ParentWorkspaceId, item.Path);

    private static (string Name, string? Description, string Metadata, DateTimeOffset UpdatedAt) DetailsFingerprint(WorkspacePathLookup item) =>
        (item.Workspace.Name, item.Workspace.Description, item.Workspace.MetadataJson, item.Workspace.UpdatedAt);

    private static bool IsWorkspaceCompetition(Exception error)
    {
        var postgres = error as PostgresException ?? (error as DbUpdateException)?.InnerException as PostgresException;
        return postgres?.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected ||
            postgres?.SqlState == PostgresErrorCodes.UniqueViolation && postgres.ConstraintName is
                "ux_workspace_subject_bindings_subject" or "ux_workspaces_root_slug" or "ux_workspaces_child_slug";
    }
}
