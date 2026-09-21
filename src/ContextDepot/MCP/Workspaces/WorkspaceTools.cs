using System.ComponentModel;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.MCP.Shared;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;

namespace ContextDepot.MCP.Workspaces;

public sealed class WorkspaceTools(ILogger<WorkspaceTools> logger)
{
    [McpServerTool(Name = "workspace_list", Title = "List workspaces", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("List current-depot workspaces for explicit management. Ordinary task retrieval does not require this call.")]
    public async Task<IReadOnlyList<WorkspaceModel>> ListAsync(IWorkspaceAppService service, string? parentPath = null, CancellationToken cancellationToken = default)
    {
        return await MCPToolExecutor.ExecuteAsync(
            () => service.ListAsync(parentPath, cancellationToken),
            logger);
    }

    [McpServerTool(Name = "workspace_upsert", Title = "Create or update workspace", Destructive = false, UseStructuredContent = true)]
    [Description("Create or update a stable current-depot workspace path. Renaming and moving workspaces are not supported.")]
    public async Task<WorkspaceModel> UpsertAsync(IWorkspaceAppService service, string path, string name, string? description = null, string metadataJson = "{}", bool createMissingParents = false, CancellationToken cancellationToken = default)
    {
        return await MCPToolExecutor.ExecuteAsync(
            () => service.UpsertAsync(new UpsertWorkspaceCommand(path, name, description, metadataJson, createMissingParents), cancellationToken),
            logger);
    }
}
