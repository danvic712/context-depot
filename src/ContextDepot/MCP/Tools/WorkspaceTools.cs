using ContextDepot.Application.Workspaces.Enums;
using System.ComponentModel;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ModelContextProtocol.Server;

namespace ContextDepot.MCP.Tools;

public sealed class WorkspaceTools(MCPToolExecutor executor, ILogger<WorkspaceTools> logger)
{
    [McpServerTool(Name = "workspace_list", Title = "List workspaces", ReadOnly = true, Idempotent = true,
        UseStructuredContent = true)]
    [Description(
        "List current-depot workspaces for explicit management. Ordinary task retrieval does not require this call.")]
    public async Task<IReadOnlyList<WorkspaceModel>> ListAsync(IWorkspaceAppService service, string? parentPath = null,
        CancellationToken cancellationToken = default)
    {
        return await executor.ExecuteAsync(
            () => service.ListAsync(parentPath, cancellationToken),
            logger);
    }

    [McpServerTool(Name = "workspace_upsert", Title = "Create or update workspace", Destructive = false,
        UseStructuredContent = true)]
    [Description(
        "Create or update a stable Workspace path. Use mode=ensure with a declared subject to bind or reuse an authorized target, or atomically create and grant a new Workspace. Ensure never overwrites existing management fields and requires an existing parent. Creation does not grant access to existing ungranted Workspaces.")]
    public async Task<WorkspaceWriteResult> UpsertAsync(IWorkspaceAppService service, string path, string name,
        string? description = null, string metadataJson = "{}", bool createMissingParents = false,
        WorkspaceUpsertMode mode = WorkspaceUpsertMode.Upsert, WorkspaceSubject? subject = null,
        CancellationToken cancellationToken = default)
    {
        return await executor.ExecuteAsync(async () =>
        {
            if (!Enum.IsDefined(mode) || mode == WorkspaceUpsertMode.Ensure && (subject is null || createMissingParents) ||
                mode == WorkspaceUpsertMode.Upsert && subject is not null)
                throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidRequest);
            return mode == WorkspaceUpsertMode.Ensure
                ? await service.EnsureAsync(new(subject!, path, name, description, metadataJson), cancellationToken)
                : new WorkspaceWriteResult(await service.UpsertAsync(new(path, name, description, metadataJson, createMissingParents), cancellationToken));
        }, logger);
    }
}
