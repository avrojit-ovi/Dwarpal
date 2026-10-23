using System.Globalization;
using Dwarpal.Core.Models;

namespace Dwarpal.Core.Data;

public sealed class ProtectedAppRepository
{
    private const string Columns =
        "Id, DisplayName, ExecutablePath, ProcessName, IsEnabled, CreatedUtc, LastLockedUtc, AutoRelockMinutes";

    private readonly DwarpalDatabase _database;

    public ProtectedAppRepository(DwarpalDatabase database) => _database = database;

    public async Task<IReadOnlyList<ProtectedApp>> GetAllAsync(
        bool enabledOnly = false,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = enabledOnly
            ? $"SELECT {Columns} FROM ProtectedApps WHERE IsEnabled = 1 ORDER BY DisplayName COLLATE NOCASE;"
            : $"SELECT {Columns} FROM ProtectedApps ORDER BY DisplayName COLLATE NOCASE;";

        var apps = new List<ProtectedApp>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            apps.Add(Map(reader));
        }

        return apps;
    }

    public async Task<ProtectedApp?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM ProtectedApps WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<bool> ExistsAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM ProtectedApps WHERE ExecutablePath = $path COLLATE NOCASE;";
        command.Parameters.AddWithValue("$path", NormalizePath(executablePath));

        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    /// <summary>
    /// Add ba update: same path already thakle display name refresh hoy. Path normalization
    /// e full path + filename theke process name ber kora hoy.
    /// </summary>
    public async Task<ProtectedApp> AddAsync(
        string displayName,
        string executablePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var normalizedPath = NormalizePath(executablePath);
        var processName = Path.GetFileNameWithoutExtension(normalizedPath);
        var trimmedName = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileNameWithoutExtension(normalizedPath)
            : displayName.Trim();

        await using var connection = await _database.OpenAsync(cancellationToken);

        // Unique index ta COLLATE NOCASE, tai upsert target er bodole explicit select-then-write.
        long existingId;
        await using (var existing = connection.CreateCommand())
        {
            existing.CommandText = "SELECT Id FROM ProtectedApps WHERE ExecutablePath = $path COLLATE NOCASE LIMIT 1;";
            existing.Parameters.AddWithValue("$path", normalizedPath);
            var result = await existing.ExecuteScalarAsync(cancellationToken);
            existingId = result is null or DBNull ? 0 : Convert.ToInt64(result);
        }

        await using (var write = connection.CreateCommand())
        {
            if (existingId > 0)
            {
                write.CommandText = "UPDATE ProtectedApps SET DisplayName = $name, ProcessName = $process WHERE Id = $id;";
                write.Parameters.AddWithValue("$id", existingId);
            }
            else
            {
                write.CommandText = """
                    INSERT INTO ProtectedApps (DisplayName, ExecutablePath, ProcessName, IsEnabled, CreatedUtc)
                    VALUES ($name, $path, $process, 1, $created);
                    """;
                write.Parameters.AddWithValue("$path", normalizedPath);
                write.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("o"));
            }

            write.Parameters.AddWithValue("$name", trimmedName);
            write.Parameters.AddWithValue("$process", processName);
            await write.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var select = connection.CreateCommand();
        select.CommandText = $"SELECT {Columns} FROM ProtectedApps WHERE ExecutablePath = $path COLLATE NOCASE;";
        select.Parameters.AddWithValue("$path", normalizedPath);

        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Protected app could not be saved.");
        }

        return Map(reader);
    }

    public async Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ProtectedApps WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task SetEnabledAsync(int id, bool enabled, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ProtectedApps SET IsEnabled = $enabled WHERE Id = $id;";
        command.Parameters.AddWithValue("$enabled", enabled ? 1 : 0);
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetAutoRelockAsync(int id, int? minutes, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ProtectedApps SET AutoRelockMinutes = $minutes WHERE Id = $id;";
        command.Parameters.AddWithValue("$minutes", (object?)minutes ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkLockedAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ProtectedApps SET LastLockedUtc = $locked WHERE Id = $id;";
        command.Parameters.AddWithValue("$locked", DateTimeOffset.UtcNow.ToString("o"));
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var trimmed = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        return Path.GetFullPath(trimmed);
    }

    private static ProtectedApp Map(Microsoft.Data.Sqlite.SqliteDataReader reader) => new(
        reader.GetInt32(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetInt32(4) != 0,
        DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        reader.IsDBNull(6)
            ? null
            : DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        reader.IsDBNull(7) ? null : reader.GetInt32(7));
}
