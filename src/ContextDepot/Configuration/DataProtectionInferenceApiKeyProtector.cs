using ContextDepot.Infrastructure.Configuration;
using Microsoft.AspNetCore.DataProtection;

namespace ContextDepot.Configuration;

public sealed class DataProtectionInferenceApiKeyProtector : IInferenceApiKeyProtector
{
    private const string Purpose = "ContextDepot.InferenceProvider.ApiKey.v1";
    private readonly IDataProtector protector;

    public DataProtectionInferenceApiKeyProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        return protector.Protect(plaintext);
    }

    public string Unprotect(string protectedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedValue);
        return protector.Unprotect(protectedValue);
    }
}
