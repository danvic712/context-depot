using ContextDepot.Infrastructure.Access;

namespace ContextDepot.Infrastructure.Contracts;

public interface IAccessKeyAuthenticator
{
    Task<AccessKeyIdentity?> AuthenticateAsync(
        string presentedKey,
        CancellationToken cancellationToken = default);
}
