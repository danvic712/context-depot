using System.Text.Json;
using ContextDepot.Application.Abstractions;
using ContextDepot.Application.Persistence;
using ContextDepot.Domain.Entities;

namespace ContextDepot.Application.Workspaces;

public sealed class WorkspaceAppService(
    ICurrentOwnerContext currentOwner,
    IWorkspaceRepository repository,
    IIdGenerator idGenerator,
    IClock clock) : IWorkspaceAppService
{
    public async Task<WorkspaceModel?> GetAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var workspace = await repository.GetByIdAsync(currentOwner.OwnerId, workspaceId, cancellationToken);
        return workspace is null ? null : WorkspaceModelMapper.ToModel(workspace, workspace.Slug);
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
        var entities = await repository.ListAsync(currentOwner.OwnerId, normalizedParent, cancellationToken);
        var prefix = normalizedParent is null ? null : normalizedParent + "/";
        return entities.Select(entity => WorkspaceModelMapper.ToModel(entity, prefix is null ? entity.Slug : prefix + entity.Slug)).ToArray();
    }

    public async Task<WorkspaceModel> UpsertAsync(UpsertWorkspaceCommand command, CancellationToken cancellationToken)
    {
        var normalizedPath = WorkspacePath.Normalize(command.Path);
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ContextDepotApplicationException("InvalidWorkspaceName", "Workspace name is required.");
        }

        ValidateMetadata(command.MetadataJson);

        var existing = await repository.GetByPathAsync(currentOwner.OwnerId, normalizedPath, cancellationToken);
        if (existing is not null)
        {
            existing.Update(command.Name.Trim(), command.Description?.Trim(), command.MetadataJson, clock.UtcNow);
            repository.Update(existing);
            await repository.SaveChangesAsync(cancellationToken);
            return WorkspaceModelMapper.ToModel(existing, normalizedPath);
        }

        var segments = WorkspacePath.Segments(normalizedPath);
        Guid? parentId = null;
        string currentPath = string.Empty;
        Workspace? created = null;
        for (var index = 0; index < segments.Count; index++)
        {
            currentPath = string.IsNullOrEmpty(currentPath) ? segments[index] : currentPath + "/" + segments[index];
            var isLeaf = index == segments.Count - 1;
            var candidate = await repository.GetByPathAsync(currentOwner.OwnerId, currentPath, cancellationToken);
            if (candidate is not null)
            {
                parentId = candidate.Id;
                if (isLeaf)
                {
                    candidate.Update(command.Name.Trim(), command.Description?.Trim(), command.MetadataJson, clock.UtcNow);
                    repository.Update(candidate);
                    created = candidate;
                }

                continue;
            }

            if (!isLeaf && !command.CreateMissingParents)
            {
                throw new ContextDepotApplicationException("WorkspaceParentNotFound", $"Workspace parent '{currentPath}' does not exist.");
            }

            var now = clock.UtcNow;
            var name = isLeaf ? command.Name.Trim() : segments[index];
            var workspace = new Workspace(idGenerator.NewId(), currentOwner.OwnerId, parentId, name, segments[index], isLeaf ? command.Description?.Trim() : null, now);
            workspace.Update(name, isLeaf ? command.Description?.Trim() : null, isLeaf ? command.MetadataJson : "{}", now);
            await repository.AddAsync(workspace, cancellationToken);
            parentId = workspace.Id;
            if (isLeaf)
            {
                created = workspace;
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
        return WorkspaceModelMapper.ToModel(created ?? throw new InvalidOperationException("Workspace creation did not produce a workspace."), normalizedPath);
    }

    private static void ValidateMetadata(string metadataJson)
    {
        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException();
            }
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            throw new ContextDepotApplicationException("InvalidWorkspaceMetadata", "Workspace metadata must be a JSON object.");
        }
    }
}
