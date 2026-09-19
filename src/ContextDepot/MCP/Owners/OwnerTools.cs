using System.ComponentModel;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ModelContextProtocol.Server;

namespace ContextDepot.MCP.Owners;

public sealed class OwnerTools
{
    [McpServerTool(Name = "owner_get", Title = "Read current owner", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Read the configured current owner profile when explicitly needed. Ordinary tasks should not call this before context_bootstrap.")]
    public static object Get(ICurrentOwnerContext owner) => new { ownerId = owner.OwnerId, displayName = owner.DisplayName };
}
