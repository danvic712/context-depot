using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;

namespace ContextDepot.Application.Shared.Runtime;

/// <summary>
/// Authorizes deployment management operations independently of MCP workspace grants.
/// </summary>
public static class ManagementAccess
{
    /// <summary>
    /// Requires trusted Web management access.
    /// </summary>
    /// <param name="access">
    /// The access scope that must represent trusted internal management.
    /// </param>
    /// <exception cref="ContextDepotApplicationException">
    /// The request does not have management access.
    /// </exception>
    public static void RequireManagement(IWorkspaceAccessContext access)
    {
        if (!access.HasUnrestrictedAccess || access.AccessKeyId is not null)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsForbidden);
    }
}
