using Dwarpal.Core.Data;
using Dwarpal.Core.Models;
using Dwarpal.Core.Security;

namespace Dwarpal.Core.Tests;

public class DatabaseTests
{
    [Fact]
    public async Task Initialize_IsIdempotent_AndCreatesSchema()
    {
        await using var fixture = await TestDatabase.CreateAsync();

        // Duitar bar init korleo fail kora jabe na.
        await fixture.Database.InitializeAsync();
        await fixture.Database.InitializeAsync();

        await using var connection = await fixture.Database.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name IN ('Settings', 'Credential', 'ActivityLog');";

        Assert.Equal(3L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Settings_RoundTrip()
    {
        await using var fixture = await TestDatabase.CreateAsync();

        await fixture.Settings.SetAsync(SettingKeys.RecoveryKeyAcknowledgedUtc, "2026-01-01T00:00:00.0000000+00:00");

        Assert.Equal("2026-01-01T00:00:00.0000000+00:00",
            await fixture.Settings.GetAsync(SettingKeys.RecoveryKeyAcknowledgedUtc));
        Assert.Null(await fixture.Settings.GetAsync("missing.key"));
        Assert.Equal("fallback", await fixture.Settings.GetAsync("missing.key", "fallback"));
        Assert.True(await fixture.Settings.GetBoolAsync("missing.bool", true));
    }

    [Fact]
    public async Task CredentialHashes_AreEncryptedAtRest()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await fixture.CredentialService.SetupAsync(CredentialKind.Pin, "4821");

        await using var connection = await fixture.Database.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT SecretHash, RecoveryHash FROM Credential WHERE Id = 1;";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        var storedSecretHash = reader.GetString(0);
        var storedRecoveryHash = reader.GetString(1);

        Assert.NotEmpty(storedSecretHash);
        Assert.NotEmpty(storedRecoveryHash);
        Assert.DoesNotContain("argon2id", storedSecretHash, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("argon2id", storedRecoveryHash, StringComparison.OrdinalIgnoreCase);

        // DPAPI round-trip kaj korche kina.
        var protector = new DpapiProtector();
        Assert.StartsWith("$argon2id$", protector.Unprotect(storedSecretHash), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActivityRepository_FiltersByDateSearchAndCounts()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var now = DateTimeOffset.UtcNow;

        await fixture.Activity.AddAsync(new ActivityEntryDraft(
            now, ActivityEventType.Unlock, ActivityOutcome.Failure, "Notepad", @"C:\Windows\notepad.exe", "wrong credential"));
        await fixture.Activity.AddAsync(new ActivityEntryDraft(
            now, ActivityEventType.Lock, ActivityOutcome.Success, "Notepad", @"C:\Windows\notepad.exe"));
        await fixture.Activity.AddAsync(new ActivityEntryDraft(
            now.AddDays(-2), ActivityEventType.Unlock, ActivityOutcome.Success, "calc", @"C:\Windows\calc.exe"));

        var all = await fixture.Activity.QueryAsync();
        Assert.Equal(3, all.Count);

        var recent = await fixture.Activity.QueryAsync(fromUtc: now.AddHours(-1));
        Assert.Equal(2, recent.Count);

        var search = await fixture.Activity.QueryAsync(search: "notepad");
        Assert.Equal(2, search.Count);

        var failures = await fixture.Activity.CountAsync(ActivityEventType.Unlock, ActivityOutcome.Failure);
        Assert.Equal(1, failures);

        await fixture.Activity.ClearAsync();
        Assert.Empty(await fixture.Activity.QueryAsync());
    }
}
