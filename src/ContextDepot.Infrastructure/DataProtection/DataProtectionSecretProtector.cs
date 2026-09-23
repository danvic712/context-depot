using System.Security.Cryptography;
using ContextDepot.Application.DataProtection;
using ContextDepot.Application.DataProtection.Enums;
using Microsoft.AspNetCore.DataProtection;

namespace ContextDepot.Infrastructure.DataProtection;

public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider)
    : ISecretProtector
{
    private const string InferenceProviderApiKeyPurpose = "ContextDepot.InferenceProvider.ApiKey.v1";

    public string Protect(string plaintext, SecretProtectionPurpose purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        return CreateProtector(purpose).Protect(plaintext);
    }

    public bool TryUnprotect(
        string protectedValue,
        SecretProtectionPurpose purpose,
        out string? plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedValue);

        try
        {
            plaintext = CreateProtector(purpose).Unprotect(protectedValue);
            return true;
        }
        catch (CryptographicException)
        {
            plaintext = null;
            return false;
        }
    }

    private IDataProtector CreateProtector(SecretProtectionPurpose purpose) =>
        provider.CreateProtector(purpose switch
        {
            SecretProtectionPurpose.InferenceProviderApiKey => InferenceProviderApiKeyPurpose,
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null)
        });
}
