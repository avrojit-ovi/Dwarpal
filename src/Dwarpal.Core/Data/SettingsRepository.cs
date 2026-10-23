using Microsoft.Data.Sqlite;

namespace Dwarpal.Core.Data;

public static class SettingKeys
{
    /// <summary>Recovery key user copy/print kore confirm korar somoy (UTC ISO).</summary>
    public const string RecoveryKeyAcknowledgedUtc = "security.recoveryKeyAcknowledgedUtc";

    /// <summary>Global auto re-lock timeout in minutes (-1 = never).</summary>
    public const string AutoRelockDefaultMinutes = "autoRelock.defaultMinutes";

    /// <summary>Unlock er por grace period seconds.</summary>
    public const string AutoRelockGraceSeconds = "autoRelock.graceSeconds";

    /// <summary>Task Scheduler at-logon entry enabled kina.</summary>
    public const string AutoStartEnabled = "autoStart.enabled";

    /// <summary>Tamper watchdog scheduled task enabled kina.</summary>
    public const string WatchdogEnabled = "tamper.watchdog";

    /// <summary>Lockout state: consecutive failed attempts, strike count ar cooldown end.</summary>
    public const string LockoutFailedAttempts = "security.lockout.failedAttempts";
    public const string LockoutStrikes = "security.lockout.strikes";
    public const string LockoutUntilUtc = "security.lockout.untilUtc";

    /// <summary>Repeated failed attempts e intruder selfie capture enabled kina.</summary>
    public const string SelfieEnabled = "security.selfie";

    /// <summary>Active profile id; 0 mane kono profile nei - sob enabled app protect hoy.</summary>
    public const string ActiveProfileId = "profiles.activeId";
}

public sealed class SettingsRepository
{
    private readonly DwarpalDatabase _database;

    public SettingsRepository(DwarpalDatabase database) => _database = database;

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = $key LIMIT 1;";
        command.Parameters.AddWithValue("$key", key);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    public async Task<string> GetAsync(string key, string fallback, CancellationToken cancellationToken = default)
        => await GetAsync(key, cancellationToken) ?? fallback;

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Settings (Key, Value, UpdatedUtc)
            VALUES ($key, $value, $updated)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value, UpdatedUtc = excluded.UpdatedUtc;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("o"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken cancellationToken = default)
    {
        var value = await GetAsync(key, cancellationToken);
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken cancellationToken = default)
    {
        var value = await GetAsync(key, cancellationToken);
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    public Task SetIntAsync(string key, int value, CancellationToken cancellationToken = default)
        => SetAsync(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);

    public Task SetBoolAsync(string key, bool value, CancellationToken cancellationToken = default)
        => SetAsync(key, value ? "true" : "false", cancellationToken);
}
