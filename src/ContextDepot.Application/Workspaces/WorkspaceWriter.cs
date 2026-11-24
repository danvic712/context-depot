using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;

namespace ContextDepot.Application.Workspaces;

public sealed class WorkspaceWriter(IWorkspaceRepository repository, IIdGenerator ids, IWorkspaceAccessContext access)
{
    public Task<WorkspaceWritePlan> EnsureAsync(EnsureWorkspaceCommand command,
        DateTimeOffset now, CancellationToken cancellationToken) => ApplyAsync(command.Subject,
            snapshot => WorkspaceEnsurePolicy.Ensure(snapshot, command, ids, now), now, cancellationToken);

    public Task<WorkspaceWritePlan> UpsertAsync(UpsertWorkspaceCommand command,
        DateTimeOffset now, CancellationToken cancellationToken) => ApplyAsync(null,
            snapshot => WorkspaceEnsurePolicy.Upsert(snapshot, command, ids, now), now, cancellationToken);

    private async Task<WorkspaceWritePlan> ApplyAsync(WorkspaceSubject? subject,
        Func<WorkspaceWriteSnapshot, WorkspaceWritePlan> prepare, DateTimeOffset now, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var snapshot = await repository.LoadEnsureSnapshotAsync(subject, cancellationToken);
            var plan = prepare(snapshot);
            var result = await repository.TryApplyEnsureAsync(plan, now, cancellationToken);
            if (!result.Applied) continue;
            // A retry may reuse a Workspace created concurrently by this same Key.
            var verifiedIds = plan.Expected.Access.WorkspaceIds.Concat(plan.Created.Select(item => item.Id)).Distinct().ToArray();
            var topology = new WorkspaceTopology(plan.Expected.Workspaces.Select(item => item.Workspace)
                .Concat(plan.Created).Select(item => new WorkspaceTreeNode(item.Id, item.ParentWorkspaceId, item.Slug)));
            access.UpdateWorkspaceAccess(verifiedIds, topology.AncestorsIncludingSelf(verifiedIds).ToArray(),
                plan.Expected.Access.CanCreateRootWorkspaces, result.AccessVersion);
            return plan;
        }
        throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceConcurrencyConflict);
    }
}
