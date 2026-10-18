using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Shared.Validation;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Workspaces.Enums;

namespace ContextDepot.Application.Workspaces;

public sealed class WorkspaceAppService(
    ICurrentDepotContext currentDepot,
    IWorkspaceRepository repository,
    TimeProvider timeProvider,
    ISourceSafetyService sourceSafety,
    WorkspaceWriter writer) : IWorkspaceAppService
{
    public async Task<WorkspaceModel?> GetAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var lookup = await repository.GetByIdWithPathAsync(currentDepot.DepotId, workspaceId, cancellationToken);
        return lookup is null ? null : WorkspaceModelMapper.ToModel(lookup.Workspace, lookup.Path);
    }

    public async Task<WorkspaceModel?> ResolveAsync(string path, CancellationToken cancellationToken)
    {
        var normalizedPath = WorkspacePath.Normalize(path);
        var workspace = await repository.GetByPathAsync(currentDepot.DepotId, normalizedPath, cancellationToken);
        return workspace is null ? null : WorkspaceModelMapper.ToModel(workspace, normalizedPath);
    }

    public async Task<IReadOnlyList<WorkspaceModel>> ListAsync(string? parentPath, CancellationToken cancellationToken)
    {
        var normalizedParent = string.IsNullOrWhiteSpace(parentPath) ? null : WorkspacePath.Normalize(parentPath);
        var entities = await repository.ListWithPathsAsync(currentDepot.DepotId, normalizedParent, cancellationToken);
        return entities
            .Select(lookup => WorkspaceModelMapper.ToModel(lookup.Workspace, lookup.Path))
            .ToArray();
    }

    public Task<WorkspaceTopology> LoadTopologyAsync(CancellationToken cancellationToken) =>
        repository.LoadVisibleTopologyAsync(currentDepot.DepotId, cancellationToken);

    /// <summary>Lists readable named scopes without restricting them to a parent level.</summary>
    public async Task<IReadOnlyList<WorkspaceModel>> ListVisibleAsync(CancellationToken cancellationToken)
    {
        var entities = await repository.ListVisibleWithPathsAsync(currentDepot.DepotId, cancellationToken);
        return entities.Select(lookup => WorkspaceModelMapper.ToModel(lookup.Workspace, lookup.Path)).ToArray();
    }

    public async Task<WorkspaceModel> UpsertAsync(UpsertWorkspaceCommand command, CancellationToken cancellationToken)
    {
        var (path, name, description) = NormalizeWrite(command.Path, command.Name, command.Description, command.MetadataJson);
        var plan = await writer.UpsertAsync(currentDepot.DepotId, command with
        {
            Path = path, Name = name, Description = description
        }, timeProvider.GetUtcNow(), cancellationToken);
        return WorkspaceModelMapper.ToModel(plan.Target.Workspace, plan.Target.Path);
    }

    public async Task<WorkspaceSubjectResolution> ResolveSubjectAsync(WorkspaceSubject subject, CancellationToken cancellationToken)
    {
        var normalized = WorkspaceSubjectNormalizer.Normalize(subject, sourceSafety);
        var snapshot = await repository.LoadEnsureSnapshotAsync(currentDepot.DepotId, normalized, cancellationToken);
        var target = snapshot.Binding is null ? null :
            snapshot.Workspaces.SingleOrDefault(item => item.Workspace.Id == snapshot.Binding.WorkspaceId);
        var status = !snapshot.Access.IsActive ? WorkspaceSubjectStatus.Unavailable :
            snapshot.Binding is null ? WorkspaceSubjectStatus.NeedsResolution :
            target is not null && snapshot.Access.CanAccess(target.Workspace.Id) ? WorkspaceSubjectStatus.Resolved :
            WorkspaceSubjectStatus.Unavailable;
        return new(normalized, status, status == WorkspaceSubjectStatus.Resolved ?
            WorkspaceModelMapper.ToModel(target!.Workspace, target.Path) : null,
            snapshot.Access.IsActive && (snapshot.Access.Unrestricted || snapshot.Access.CanCreateRootWorkspaces));
    }

    public async Task<WorkspaceWriteResult> EnsureAsync(EnsureWorkspaceCommand command, CancellationToken cancellationToken)
    {
        var subject = WorkspaceSubjectNormalizer.Normalize(command.Subject, sourceSafety);
        var (path, name, description) = NormalizeWrite(command.Path, command.Name, command.Description, command.MetadataJson);
        var plan = await writer.EnsureAsync(currentDepot.DepotId, command with
        {
            Subject = subject, Path = path, Name = name, Description = description
        }, timeProvider.GetUtcNow(), cancellationToken);
        var outcome = plan.Outcome == WorkspaceUpsertPersistenceOutcome.Created ? EnsureWorkspaceOutcome.Created :
            plan.NewBinding is not null ? EnsureWorkspaceOutcome.BoundExisting : EnsureWorkspaceOutcome.ReusedExisting;
        return new(WorkspaceModelMapper.ToModel(plan.Target.Workspace, plan.Target.Path), outcome);
    }

    private (string Path, string Name, string? Description) NormalizeWrite(string path, string name,
        string? description, string metadataJson)
    {
        var normalizedPath = WorkspacePath.Normalize(path);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidWorkspaceName);
        sourceSafety.EnsureSafe(name);
        sourceSafety.EnsureSafe(description);
        sourceSafety.EnsureSafe(metadataJson);
        JsonObjectValidator.EnsureObject(metadataJson, ApplicationErrorCodes.InvalidWorkspaceMetadata);
        return (normalizedPath, name.Trim(), description?.Trim());
    }
}
