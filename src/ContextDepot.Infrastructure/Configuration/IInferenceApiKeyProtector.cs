namespace ContextDepot.Infrastructure.Configuration;

public interface IInferenceApiKeyProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
