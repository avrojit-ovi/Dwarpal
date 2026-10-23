using Dwarpal.Core.Models;
using Dwarpal.Core.Security;

namespace Dwarpal.Core.Tests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("1234")]
    [InlineData("12345678")]
    public void Pin_WithFourToEightDigits_IsValid(string pin)
    {
        Assert.True(PasswordPolicy.Validate(pin, CredentialKind.Pin).IsValid);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789")]
    [InlineData("12a4")]
    [InlineData("12 4")]
    [InlineData("")]
    [InlineData(null)]
    public void Pin_InvalidValues_AreRejected(string? pin)
    {
        var result = PasswordPolicy.Validate(pin, CredentialKind.Pin);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Theory]
    [InlineData("dwarpal123")]
    [InlineData("Guard#2026")]
    [InlineData("a1bcdefg")]
    public void Password_WithLetterAndDigit_IsValid(string password)
    {
        Assert.True(PasswordPolicy.Validate(password, CredentialKind.Password).IsValid);
    }

    [Theory]
    [InlineData("abc123")]          // too short
    [InlineData("abcdefgh")]        // no digit
    [InlineData("12345678")]        // no letter
    [InlineData(" dwarpal123")]     // leading space
    [InlineData("dwarpal123 ")]     // trailing space
    [InlineData("")]
    public void Password_InvalidValues_AreRejected(string password)
    {
        Assert.False(PasswordPolicy.Validate(password, CredentialKind.Password).IsValid);
    }

    [Fact]
    public void Password_LongerThanMax_IsRejected()
    {
        var password = "a1" + new string('x', PasswordPolicy.PasswordMaxLength);

        Assert.False(PasswordPolicy.Validate(password, CredentialKind.Password).IsValid);
    }
}
