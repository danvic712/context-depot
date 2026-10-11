using System.Globalization;
using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Settings;

public sealed class AccessKeyAppService(IAccessKeyRepository repository, IAccessKeySecretGenerator secrets,
    ICurrentDepotContext depot, IWorkspaceAccessContext access, IIdGenerator ids, TimeProvider clock) : IAccessKeyAppService
{
    public Task<AccessKeyListDto> ListAsync(CancellationToken cancellationToken)
    {
        SettingsAccess.RequireManagement(depot, access);
        return repository.ListAsync(depot.DepotId, cancellationToken);
    }

    public async Task<IssuedAccessKeyDto> CreateAsync(CreateAccessKeyRequest request, CancellationToken cancellationToken)
    {
        SettingsAccess.RequireManagement(depot, access);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidRequest);
        var grants = await ValidateGrantsAsync(request.WorkspaceIds, request.CanCreateRootWorkspaces, cancellationToken);
        var secret = secrets.Generate();
        var now = clock.GetUtcNow();
        var key = new DepotAccessKey(ids.NewId(), depot.DepotId, request.Name.Trim(), secret.Prefix, secret.SecretHash, now,
            request.CanCreateRootWorkspaces);
        foreach (var workspaceId in grants)
            key.WorkspaceGrants.Add(new WorkspaceAccessGrant(key.Id, depot.DepotId, workspaceId, now));
        return new IssuedAccessKeyDto(await repository.CreateAsync(key, cancellationToken), secret.Plaintext);
    }

    public async Task<AccessKeyDto> RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        SettingsAccess.RequireManagement(depot, access);
        return await repository.RevokeAsync(depot.DepotId, id, clock.GetUtcNow(), cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.AccessKeyNotFound);
    }

    public async Task<IssuedAccessKeyDto> RotateAsync(Guid id, CancellationToken cancellationToken)
    {
        SettingsAccess.RequireManagement(depot, access);
        var secret = secrets.Generate();
        var key = await repository.RotateAsync(depot.DepotId, id, secret, ids.NewId(), clock.GetUtcNow(), cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.AccessKeyNotFound);
        return new IssuedAccessKeyDto(key, secret.Plaintext);
    }

    public async Task<AccessKeyDto> SetGrantsAsync(Guid id, UpdateAccessKeyGrantsRequest request, CancellationToken cancellationToken)
    {
        SettingsAccess.RequireManagement(depot, access);
        long? version = null;
        if (request.ExpectedAccessVersion is not null)
        {
            if (!long.TryParse(request.ExpectedAccessVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
                throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidRequest);
            version = parsed;
        }
        // The locked current capability determines whether an empty set is allowed.
        var grants = await ValidateGrantsAsync(request.WorkspaceIds, true, cancellationToken);
        return await repository.SetAccessAsync(depot.DepotId, id,
            new(grants, request.CanCreateRootWorkspaces, version), clock.GetUtcNow(), cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.AccessKeyNotFound);
    }

    private async Task<Guid[]> ValidateGrantsAsync(Guid[]? workspaceIds, bool allowEmpty, CancellationToken cancellationToken)
    {
        if (workspaceIds is null || (!allowEmpty && workspaceIds.Length == 0) || workspaceIds.Length > 2000 || workspaceIds.Contains(Guid.Empty))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidRequest);
        var grants = workspaceIds.Distinct().Order().ToArray();
        if (grants.Length > 0 && !await repository.WorkspacesBelongToDepotAsync(depot.DepotId, grants, cancellationToken))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidRequest);
        return grants;
    }
}
