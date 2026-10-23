using Dwarpal.Core.Security;

namespace Dwarpal.Core.Tests;

public class Argon2idHasherTests
{
    private static Argon2idHasher CreateHasher() => new(memoryKb: 8192, iterations: 1, parallelism: 1);

    [Fact]
    public void Hash_ThenVerify_ReturnsTrue()
    {
        var hasher = CreateHasher();
        var encoded = hasher.Hash("1234");

        Assert.True(hasher.Verify("1234", encoded));
    }

    [Fact]
    public void Verify_WithWrongSecret_ReturnsFalse()
    {
        var hasher = CreateHasher();
        var encoded = hasher.Hash("1234");

        Assert.False(hasher.Verify("4321", encoded));
        Assert.False(hasher.Verify(string.Empty, encoded));
    }

    [Fact]
    public void Hash_NeverStoresPlainSecret()
    {
        var hasher = CreateHasher();
        var encoded = hasher.Hash("Sup3rSecret");

        Assert.DoesNotContain("Sup3rSecret", encoded, StringComparison.Ordinal);
        Assert.StartsWith("$argon2id$v=19$m=8192,t=1,p=1$", encoded, StringComparison.Ordinal);
    }

    [Fact]
    public void Hash_UsesRandomSalt_SoSameSecretProducesDifferentHashes()
    {
        var hasher = CreateHasher();

        var first = hasher.Hash("1234");
        var second = hasher.Hash("1234");

        Assert.NotEqual(first, second);
        Assert.True(hasher.Verify("1234", first));
        Assert.True(hasher.Verify("1234", second));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("$argon2id$v=19$m=8192,t=1,p=1$onlyfourparts$abc")]
    [InlineData("$bcrypt$v=19$m=8192,t=1,p=1$c2FsdHNhbHRzYWx0c2FsdA==$aGFzaA==")]
    public void Verify_WithMalformedHash_ReturnsFalse(string encoded)
    {
        var hasher = CreateHasher();

        Assert.False(hasher.Verify("1234", encoded));
    }

    [Fact]
    public void NeedsRehash_TrueWhenCostParametersDiffer()
    {
        var lowCost = CreateHasher();
        var encoded = lowCost.Hash("1234");

        var stronger = new Argon2idHasher(memoryKb: 16384, iterations: 2, parallelism: 1);

        Assert.False(lowCost.NeedsRehash(encoded));
        Assert.True(stronger.NeedsRehash(encoded));
        Assert.True(stronger.NeedsRehash("garbage"));
    }
}
