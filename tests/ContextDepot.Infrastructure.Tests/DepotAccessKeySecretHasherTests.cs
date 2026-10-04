using ContextDepot.Infrastructure.CurrentDepot;

namespace ContextDepot.Infrastructure.Tests;

public sealed class DepotAccessKeySecretHasherTests
{
    [Fact]
    public void Generated_key_verifies_with_either_hex_case_and_rejects_another_key()
    {
        var hasher = new DepotAccessKeySecretHasher();
        var key = hasher.Generate();

        Assert.True(hasher.Verify(key.Plaintext, key.SecretHash));
        Assert.True(hasher.Verify(key.Plaintext, key.SecretHash.ToUpperInvariant()));
        Assert.False(hasher.Verify(hasher.Generate().Plaintext, key.SecretHash));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-hex")]
    [InlineData("000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("00000000000000000000000000000000000000000000000000000000000000000g")]
    [InlineData("000000000000000000000000000000000000000000000000000000000000000000")]
    public void Invalid_or_mismatched_hash_returns_false_without_throwing(string hash)
    {
        var hasher = new DepotAccessKeySecretHasher();

        Assert.False(hasher.Verify(hasher.Generate().Plaintext, hash));
    }
}
