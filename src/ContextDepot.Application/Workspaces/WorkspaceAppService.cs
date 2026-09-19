using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Shared.Safety.Dtos;
using ContextDepot.Application.Shared.Validation;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Workspaces.Enums;

namespace ContextDepot.Application.Workspaces;

public sealed class WorkspaceAppService(
    ICurrentOwnerContext currentOwner,
    IWorkspaceRepository repository,
    TimeProvider timeProvider,
    ISourceSafetyService sourceSafety) : IWorkspaceAppService
{
    public async Task<WorkspaceModel?> GetAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var lookup = await repository.GetByIdWithPathAsync(currentOwner.OwnerId, workspaceId, cancellationToken);
        return lookup is null ? null : WorkspaceModelMapper.ToModel(lookup.Workspace, lookup.Path);
    }

    public async Task<WorkspaceModel?> ResolveAsync(string path, CancellationToken cancellationToken)
    {
        var normalizedPath = WorkspacePath.Normalize(path);
        var workspace = await repository.GetByPathAsync(currentOwner.OwnerId, normalizedPath, cancellationToken);
        return workspace is null ? null : WorkspaceModelMapper.ToModel(workspace, normalizedPath);
    }

    public async Task<IReadOnlyList<WorkspaceModel>> ListAsync(string? parentPath, CancellationToken cancellationToken)
    {
        var normalizedParent = string.IsNullOrWhiteSpace(parentPath) ? null : WorkspacePath.Normalize(parentPath);
        var entities = await repository.ListWithPathsAsync(currentOwner.OwnerId, normalizedParent, cancellationToken);
        return entities
            .Select(lookup => WorkspaceModelMapper.ToModel(lookup.Workspace, lookup.Path))
            .ToArray();
    }

    public async Task<WorkspaceModel> UpsertAsync(UpsertWorkspaceCommand command, CancellationToken cancellationToken)
    {
        var normalizedPath = WorkspacePath.Normalize(command.Path);
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidWorkspaceName);
        }

        sourceSafety.EnsureSafe(command.Name);
        sourceSafety.EnsureSafe(command.Description);
        sourceSafety.EnsureSafe(command.MetadataJson);
        JsonObjectValidator.EnsureObject(command.MetadataJson, ApplicationErrorCodes.InvalidWorkspaceMetadata);
        var result = await repository.UpsertPathAsync(
            currentOwner.OwnerId,
            normalizedPath,
            command.Name.Trim(),
            command.Description?.Trim(),
            command.MetadataJson,
            command.CreateMissingParents,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return result.Outcome switch
        {
            WorkspaceUpsertPersistenceOutcome.ParentNotFound => throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceParentNotFound),
            WorkspaceUpsertPersistenceOutcome.ConcurrencyConflict => throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceConcurrencyConflict),
            _ when result.Workspace is not null => WorkspaceModelMapper.ToModel(result.Workspace, normalizedPath),
            _ => throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceWriteFailed)
        };
    }

}
