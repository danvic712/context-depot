using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Workspaces;

public static class WorkspaceEnsurePolicy
{
    public static bool CanCreate(WorkspaceAccessSnapshot access, Guid? parentId,
        IReadOnlySet<Guid>? createdIds = null) => access.IsActive &&
        (access.Unrestricted || (parentId is null ? access.CanCreateRootWorkspaces :
            access.CanAccess(parentId.Value) || createdIds?.Contains(parentId.Value) == true));

    public static WorkspaceWritePlan Ensure(WorkspaceWriteSnapshot snapshot, EnsureWorkspaceCommand command,
        Guid depotId, IIdGenerator ids, DateTimeOffset now)
    {
        var bound = snapshot.Binding is null ? null :
            snapshot.Workspaces.SingleOrDefault(item => item.Workspace.Id == snapshot.Binding.WorkspaceId);
        if (snapshot.Binding is not null)
        {
            if (bound is null || !snapshot.Access.CanAccess(bound.Workspace.Id)) Unavailable();
            return new(snapshot, bound!, [], false, null);
        }

        var existing = snapshot.Workspaces.SingleOrDefault(item => item.Path == command.Path);
        if (existing is not null)
        {
            if (!snapshot.Access.CanAccess(existing.Workspace.Id)) Unavailable();
            return new(snapshot, existing, [], false, Binding(existing.Workspace.Id));
        }

        var split = command.Path.LastIndexOf('/');
        var parent = split < 0 ? null : snapshot.Workspaces.SingleOrDefault(item => item.Path == command.Path[..split]);
        if ((split >= 0 && parent is null) || !CanCreate(snapshot.Access, parent?.Workspace.Id)) Unavailable();
        var workspace = NewWorkspace(command.Path, command.Name, command.Description, command.MetadataJson,
            parent?.Workspace.Id, depotId, ids, now);
        return new(snapshot, new(workspace, command.Path), [workspace], false, Binding(workspace.Id));

        WorkspaceSubjectBinding Binding(Guid workspaceId) => new(ids.NewId(), depotId, workspaceId,
            command.Subject.Type, command.Subject.Key, now);
    }

    public static WorkspaceWritePlan Upsert(WorkspaceWriteSnapshot snapshot, UpsertWorkspaceCommand command,
        Guid depotId, IIdGenerator ids, DateTimeOffset now)
    {
        var existing = snapshot.Workspaces.SingleOrDefault(item => item.Path == command.Path);
        if (existing is not null)
        {
            if (!snapshot.Access.CanAccess(existing.Workspace.Id)) ParentUnavailable();
            var entity = existing.Workspace;
            if (entity.Name == command.Name && entity.Description == command.Description && entity.MetadataJson == command.MetadataJson)
                return new(snapshot, existing, [], false, null);
            // Keep the expected snapshot immutable while preparing an update.
            var updated = new Workspace(entity.Id, entity.DepotId, entity.ParentWorkspaceId, entity.Name,
                entity.Slug, entity.Description, entity.CreatedAt);
            updated.Update(command.Name, command.Description, command.MetadataJson, now);
            return new(snapshot, new(updated, command.Path), [], true, null);
        }

        var created = new List<Workspace>();
        var createdIds = new HashSet<Guid>();
        Guid? parentId = null;
        var path = string.Empty;
        foreach (var segment in command.Path.Split('/'))
        {
            path = path.Length == 0 ? segment : path + "/" + segment;
            var current = snapshot.Workspaces.SingleOrDefault(item => item.Path == path);
            if (current is not null) { parentId = current.Workspace.Id; continue; }
            var leaf = path == command.Path;
            if ((!leaf && !command.CreateMissingParents) || !CanCreate(snapshot.Access, parentId, createdIds)) ParentUnavailable();
            var workspace = NewWorkspace(path, leaf ? command.Name : segment, leaf ? command.Description : null,
                leaf ? command.MetadataJson : "{}", parentId, depotId, ids, now);
            created.Add(workspace);
            createdIds.Add(workspace.Id);
            parentId = workspace.Id;
        }
        return new(snapshot, new(created[^1], command.Path), created, false, null);
    }

    private static Workspace NewWorkspace(string path, string name, string? description, string metadata,
        Guid? parentId, Guid depotId, IIdGenerator ids, DateTimeOffset now)
    {
        var workspace = new Workspace(ids.NewId(), depotId, parentId, name, path[(path.LastIndexOf('/') + 1)..], description, now);
        workspace.Update(name, description, metadata, now);
        return workspace;
    }

    private static void Unavailable() => throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceUnavailable);
    private static void ParentUnavailable() => throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceParentNotFound);
}
