using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;

namespace ContextDepot.Application.Settings;

// Management belongs to the trusted Web deployment, never to an MCP Workspace Grant.
public static class SettingsAccess
{
    public static void RequireManagement(ICurrentDepotContext depot, IWorkspaceAccessContext access)
    {
        if (depot.DepotId == Guid.Empty || !access.HasUnrestrictedAccess || access.DepotAccessKeyId is not null)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsForbidden);
    }
}
