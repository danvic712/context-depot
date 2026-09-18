using System.ComponentModel;
using ContextDepot.Application.Workspaces;
using ModelContextProtocol.Server;

namespace ContextDepot.Mcp;

public sealed class WorkspaceTools
{
    [McpServerTool(Name = "workspace_list", Title = "List workspaces", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("List current-owner workspaces for explicit management. Ordinary task retrieval does not require this call.")]
    public async Task<IReadOnlyList<WorkspaceModel>> ListAsync(IWorkspaceAppService service, string? parentPath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await service.ListAsync(parentPath, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            McpToolErrorMapper.Throw(exception);
            throw;
        }
    }

    [McpServerTool(Name = "workspace_upsert", Title = "Create or update workspace", Destructive = false, UseStructuredContent = true)]
    [Description("Create or update a stable current-owner workspace path. Renaming and moving workspaces are not supported.")]
    public async Task<WorkspaceModel> UpsertAsync(IWorkspaceAppService service, string path, string name, string? description = null, string metadataJson = "{}", bool createMissingParents = false, CancellationToken cancellationToken = default)
    {
        try
        {
            return await service.UpsertAsync(new UpsertWorkspaceCommand(path, name, description, metadataJson, createMissingParents), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            McpToolErrorMapper.Throw(exception);
            throw;
        }
    }
}
