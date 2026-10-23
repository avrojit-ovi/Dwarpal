using Dwarpal.Core.Security;

namespace Dwarpal.Core.Tests;

public class RecoveryKeyTests
{
    [Fact]
    public void Generate_ProducesFormattedUniqueKeys()
    {
        var first = RecoveryKey.Generate();
        var second = RecoveryKey.Generate();

        Assert.Matches("^[0-9A-HJKMNP-TV-Z]{4}(-[0-9A-HJKMNP-TV-Z]{4}){7}$", first);
        Assert.NotEqual(first, second);
        Assert.True(RecoveryKey.TryNormalize(first, out _));
    }

    [Theory]
    [InlineData("abcd-efgh-jkmn-pqrs-tvwx-0123-4567-89ab")]
    [InlineData("ABCD EFGH JKMN PQRS TVWX 0123 4567 89AB")]
    [InlineData("abcdefghjkmnpqrstvwx0123456789ab")]
    public void TryNormalize_AcceptsUserNameVariants(string input)
    {
        Assert.True(RecoveryKey.TryNormalize(input, out var normalized));
        Assert.Equal("ABCD-EFGH-JKMN-PQRS-TVWX-0123-4567-89AB", normalized);
    }

    [Fact]
    public void TryNormalize_MapsConfusingCharacters()
    {
        Assert.True(RecoveryKey.TryNormalize("O000-1111-2222-3333-4444-5555-6666-7777", out var normalized));
        Assert.StartsWith("0000-1111", normalized, StringComparison.Ordinal);

        Assert.True(RecoveryKey.TryNormalize("ABCD-EFGH-JKMN-PQRS-TVWX-0123-4567-89AB".Replace("ABCD", "ABCL"), out var withL));
        Assert.StartsWith("ABC1-", withL, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TOO-SHORT")]
    [InlineData("ABCD-EFGH-JKMN-PQRS-TVWX-0123-4567-89A!")]
    [InlineData("ABCD-EFGH-JKMN-PQRS-TVWX-0123-4567-89AB-EXTRA")]
    [InlineData(null)]
    public void TryNormalize_RejectsInvalidKeys(string? input)
    {
        Assert.False(RecoveryKey.TryNormalize(input, out _));
    }
}
