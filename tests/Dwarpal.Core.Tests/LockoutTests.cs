using Dwarpal.Core.Models;
using Dwarpal.Core.Security;

namespace Dwarpal.Core.Tests;

public class LockoutTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FiveWrongAttempts_LockAccount_AndBlockCorrectSecret()
    {
        var now = Start;
        await using var fixture = await TestDatabase.CreateAsync(() => now);
        await fixture.CredentialService.SetupAsync(CredentialKind.Pin, "4821");

        for (var attempt = 1; attempt <= LockoutService.DefaultMaxAttempts - 1; attempt++)
        {
            var result = await fixture.CredentialService.VerifyAsync("0000", "App unlock");
            Assert.False(result.Success);
            Assert.Equal(0, result.LockoutSeconds);
            Assert.Contains("baki", result.Error);
        }

        var fifth = await fixture.CredentialService.VerifyAsync("0000", "App unlock");
        Assert.False(fifth.Success);
        Assert.Equal(LockoutService.BaseLockoutSeconds, fifth.LockoutSeconds);

        var status = await fixture.Lockout.GetStatusAsync();
        Assert.True(status.IsLockedOut);
        Assert.Equal(LockoutService.BaseLockoutSeconds, status.RemainingSeconds);
        Assert.Equal(1, status.Strikes);

        // Cooldown er vitore thik credential dileo unlock hobe na.
        var blocked = await fixture.CredentialService.VerifyAsync("4821", "App unlock");
        Assert.False(blocked.Success);
        Assert.True(blocked.LockoutSeconds > 0);

        now = now.AddSeconds(LockoutService.BaseLockoutSeconds + 1);

        var unlocked = await fixture.CredentialService.VerifyAsync("4821", "App unlock");
        Assert.True(unlocked.Success);

        status = await fixture.Lockout.GetStatusAsync();
        Assert.False(status.IsLockedOut);
        Assert.Equal(0, status.FailedAttempts);
    }

    [Fact]
    public async Task SuccessfulUnlock_ResetsAttemptCounter()
    {
        var now = Start;
        await using var fixture = await TestDatabase.CreateAsync(() => now);
        await fixture.CredentialService.SetupAsync(CredentialKind.Pin, "4821");

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await fixture.CredentialService.VerifyAsync("0000", "App unlock");
        }

        Assert.True((await fixture.CredentialService.VerifyAsync("4821", "App unlock")).Success);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await fixture.CredentialService.VerifyAsync("0000", "App unlock");
        }

        var status = await fixture.Lockout.GetStatusAsync();
        Assert.False(status.IsLockedOut);
        Assert.Equal(4, status.FailedAttempts);
    }

    [Fact]
    public async Task RepeatedLockouts_EscalateCooldown()
    {
        var now = Start;
        await using var fixture = await TestDatabase.CreateAsync(() => now);
        await fixture.CredentialService.SetupAsync(CredentialKind.Pin, "4821");

        await FailFiveTimesAsync(fixture);
        Assert.Equal(30, (await fixture.Lockout.GetStatusAsync()).RemainingSeconds);

        now = now.AddSeconds(31);
        await FailFiveTimesAsync(fixture);

        var second = await fixture.Lockout.GetStatusAsync();
        Assert.True(second.IsLockedOut);
        Assert.Equal(60, second.RemainingSeconds);
        Assert.Equal(2, second.Strikes);

        now = now.AddSeconds(61);
        await FailFiveTimesAsync(fixture);

        var third = await fixture.Lockout.GetStatusAsync();
        Assert.Equal(120, third.RemainingSeconds);
        Assert.Equal(3, third.Strikes);
    }

    [Fact]
    public void Duration_IsCapped()
    {
        Assert.Equal(30, LockoutService.CalculateDurationSeconds(1));
        Assert.Equal(60, LockoutService.CalculateDurationSeconds(2));
        Assert.Equal(120, LockoutService.CalculateDurationSeconds(3));
        Assert.Equal(LockoutService.MaxLockoutSeconds, LockoutService.CalculateDurationSeconds(20));
    }

    private static async Task FailFiveTimesAsync(TestDatabase fixture)
    {
        for (var attempt = 0; attempt < LockoutService.DefaultMaxAttempts; attempt++)
        {
            await fixture.CredentialService.VerifyAsync("0000", "App unlock");
        }
    }
}
