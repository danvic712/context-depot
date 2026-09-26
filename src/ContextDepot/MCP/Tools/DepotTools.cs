using System.ComponentModel;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ModelContextProtocol.Server;

namespace ContextDepot.MCP.Tools;

public sealed class DepotTools
{
    [McpServerTool(Name = "depot_get", Title = "Read current depot", ReadOnly = true, Idempotent = true,
        UseStructuredContent = true)]
    [Description(
        "Read the configured current depot profile when explicitly needed. Ordinary tasks should not call this before context_bootstrap.")]
    public static object Get(ICurrentDepotContext depot) =>
        new { depotId = depot.DepotId, displayName = depot.DisplayName };
}