using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Depots;

namespace ContextDepot.Application.Settings;

public static class AccessKeyAccessPolicy
{
    // Evaluate against the locked current Key, including grants added by automatic creation.
    public static bool Approve(DepotAccessKey key, AccessKeyAccessUpdate command)
    {
        var capability = command.CanCreateRootWorkspaces ?? key.CanCreateRootWorkspaces;
        if (!key.IsActive || (command.ExpectedAccessVersion is long expected && expected != key.AccessVersion) ||
            (command.ExpectedAccessVersion is null && (key.CanCreateRootWorkspaces || capability)))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsConflict);
        if (command.WorkspaceIds.Count == 0 && !capability)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidRequest);
        return capability;
    }
}
