using Dwarpal.Core.Models;
using Dwarpal.Core.Security;

namespace Dwarpal.Core.Tests;

public class CredentialServiceTests
{
    [Fact]
    public async Task Setup_StoresCredentialAndReturnsRecoveryKey()
    {
        await using var fixture = await TestDatabase.CreateAsync();

        var result = await fixture.CredentialService.SetupAsync(CredentialKind.Pin, "4821");

        Assert.True(result.Success);
        Assert.NotNull(result.RecoveryKey);
        Assert.True(RecoveryKey.TryNormalize(result.RecoveryKey, out _));
        Assert.True(await fixture.CredentialService.IsConfiguredAsync());
        Assert.Equal(CredentialKind.Pin, await fixture.CredentialService.GetKindAsync());
    }

    [Fact]
    public async Task Setup_RejectsWeakSecret()
    {
        await using var fixture = await TestDatabase.CreateAsync();

        var result = await fixture.CredentialService.SetupAsync(CredentialKind.Password, "abcdefgh");

        Assert.False(result.Success);
        Assert.False(await fixture.CredentialService.IsConfiguredAsync());
    }

    [Fact]
    public async Task Setup_Twice_IsRejected()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await fixture.CredentialService.SetupAsync(CredentialKind.Pin, "4821");

        var second = await fixture.CredentialService.SetupAsync(CredentialKind.Pin, "9999");

        Assert.False(second.Success);
    }

    [Fact]
    public async Task Verify_WithCorrectSecret_Succeeds_AndLogsActivity()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await fixture.CredentialService.SetupAsync(CredentialKind.Pin, "4821");

        var result = await fixture.CredentialService.VerifyAsync("4821", "App unlock");

        Assert.True(result.Success);
        var successCount = await fixture.Activity.CountAsync(ActivityEventType.Unlock, ActivityOutcome.Success);
        Assert.Equal(1, successCount);
    }

    [Fact]
    public async Task Verify_WithWrongSecret_Fails_AndLogsFailure()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await fixture.CredentialService.SetupAsync(CredentialKind.Pin, "4821");

        var result = await fixture.CredentialService.VerifyAsync("1111", "App unlock");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        var failureCount = await fixture.Activity.CountAsync(ActivityEventType.Unlock, ActivityOutcome.Failure);
        Assert.Equal(1, failureCount);
    }

    [Fact]
    public async Task ResetWithRecoveryKey_WithWrongKey_Fails()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await fixture.CredentialService.SetupAsync(CredentialKind.Pin, "4821");

        var result = await fixture.CredentialService.ResetWithRecoveryKeyAsync(
            RecoveryKey.Generate(), CredentialKind.Pin, "7777");

        Assert.False(result.Success);
        Assert.False((await fixture.CredentialService.VerifyAsync("7777", "test")).Success);
    }

    [Fact]
    public async Task ResetWithRecoveryKey_SetsNewCredential_AndRotatesRecoveryKey()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var setup = await fixture.CredentialService.SetupAsync(CredentialKind.Pin, "4821");
        var originalRecoveryKey = setup.RecoveryKey!;

        var reset = await fixture.CredentialService.ResetWithRecoveryKeyAsync(
            originalRecoveryKey, CredentialKind.Password, "newpass123");

        Assert.True(reset.Success);
        Assert.NotNull(reset.NewRecoveryKey);
        Assert.NotEqual(originalRecoveryKey, reset.NewRecoveryKey);
        Assert.True((await fixture.CredentialService.VerifyAsync("newpass123", "test")).Success);
        Assert.Equal(CredentialKind.Password, await fixture.CredentialService.GetKindAsync());

        // Purano recovery key ar cholbe na.
        var reuseOldKey = await fixture.CredentialService.ResetWithRecoveryKeyAsync(
            originalRecoveryKey, CredentialKind.Pin, "5555");
        Assert.False(reuseOldKey.Success);

        // Notun key kaj kore.
        var reuseNewKey = await fixture.CredentialService.ResetWithRecoveryKeyAsync(
            reset.NewRecoveryKey!, CredentialKind.Pin, "5555");
        Assert.True(reuseNewKey.Success);
    }

    [Fact]
    public async Task ChangeCredential_RequiresCurrentSecret()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await fixture.CredentialService.SetupAsync(CredentialKind.Pin, "4821");

        var wrongCurrent = await fixture.CredentialService.ChangeCredentialAsync("0000", CredentialKind.Pin, "8888");
        Assert.False(wrongCurrent.Success);

        var changed = await fixture.CredentialService.ChangeCredentialAsync("4821", CredentialKind.Pin, "8888");
        Assert.True(changed.Success);
        Assert.True((await fixture.CredentialService.VerifyAsync("8888", "test")).Success);
    }

    [Fact]
    public async Task RecoveryKey_ResetsCredential_AfterAppRestart()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var setup = await fixture.CredentialService.SetupAsync(CredentialKind.Pin, "4821");

        // Same DB theke notun service instance - restart simulate.
        var restarted = new CredentialService(
            fixture.Credentials,
            new Argon2idHasher(memoryKb: 8192, iterations: 1, parallelism: 1),
            new Dwarpal.Core.Services.ActivityLogger(fixture.Activity));

        var reset = await restarted.ResetWithRecoveryKeyAsync(setup.RecoveryKey!, CredentialKind.Pin, "6060");

        Assert.True(reset.Success);
    }
}
