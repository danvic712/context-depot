using ContextDepot.Infrastructure.Dtos;

namespace ContextDepot.Infrastructure.Contracts;

public interface IDepotAccessKeyAuthenticator
{
    Task<DepotAccessKeyIdentity?> AuthenticateAsync(
        string presentedKey,
        CancellationToken cancellationToken = default);
}
