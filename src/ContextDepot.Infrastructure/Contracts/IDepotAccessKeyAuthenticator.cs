using ContextDepot.Infrastructure.CurrentDepot;

namespace ContextDepot.Infrastructure.Contracts;

public interface IDepotAccessKeyAuthenticator
{
    Task<DepotAccessKeyIdentity?> AuthenticateAsync(
        string presentedKey,
        CancellationToken cancellationToken = default);
}
