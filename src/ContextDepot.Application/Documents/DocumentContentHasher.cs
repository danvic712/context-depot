using System.Security.Cryptography;
using System.Text;

namespace ContextDepot.Application.Documents;

public static class DocumentContentHasher
{
    public static string Compute(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
