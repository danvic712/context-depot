using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;

namespace ContextDepot.Application.Shared.Runtime;

/// <summary>
/// Authorizes deployment management operations independently of MCP workspace grants.
/// </summary>
public static class ManagementAccess
{
    /// <summary>
    /// Requires a selected depot and unrestricted internal access without a Depot Access Key.
    /// </summary>
    /// <param name="depot">
    /// The server-selected depot context to validate.
    /// </param>
    /// <param name="access">
    /// The access scope that must represent trusted internal management.
    /// </param>
    /// <exception cref="ContextDepotApplicationException">
    /// No depot is selected, access is restricted, or a Depot Access Key is present.
    /// </exception>
    public static void RequireManagement(ICurrentDepotContext depot, IWorkspaceAccessContext access)
    {
        if (depot.DepotId == Guid.Empty || !access.HasUnrestrictedAccess || access.DepotAccessKeyId is not null)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsForbidden);
    }
}
