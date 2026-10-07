namespace ContextDepot.Application.Settings.Contracts;

public sealed record GeneratedAccessKey(string Plaintext, string Prefix, string SecretHash)
{
    public override string ToString() => $"{nameof(GeneratedAccessKey)} {{ Secret = [REDACTED] }}";
}
