namespace ContextDepot.Infrastructure.VectorStore;

public static class VectorCollectionNamePolicy
{
    public static string CreateProfileKey(string profileFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileFingerprint);
        if (profileFingerprint.Length != 64 || !profileFingerprint.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("The embedding profile fingerprint must be a SHA-256 hex string.", nameof(profileFingerprint));
        }

        return profileFingerprint[..16].ToLowerInvariant();
    }

    public static string CreateContextCollectionName(string profileFingerprint) =>
        $"context_vectors_{CreateProfileKey(profileFingerprint)}";

    public static string CreateDocumentCollectionName(string profileFingerprint) =>
        $"document_vectors_{CreateProfileKey(profileFingerprint)}";
}
