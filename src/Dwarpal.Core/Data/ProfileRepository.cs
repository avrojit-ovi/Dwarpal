using System.Globalization;
using Dwarpal.Core.Models;

namespace Dwarpal.Core.Data;

public sealed class ProfileRepository
{
    private const string Columns =
        "p.Id, p.Name, p.CreatedUtc, " +
        "(SELECT COUNT(1) FROM ProfileApps pa WHERE pa.ProfileId = p.Id) AS AppCount";

    private readonly DwarpalDatabase _database;
    private readonly SettingsRepository _settings;

    public ProfileRepository(DwarpalDatabase database, SettingsRepository settings)
    {
        _database = database;
        _settings = settings;
    }

    public async Task<IReadOnlyList<Profile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM Profiles p ORDER BY p.Name COLLATE NOCASE;";

        var profiles = new List<Profile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            profiles.Add(Map(reader));
        }

        return profiles;
    }

    public async Task<Profile?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM Profiles p WHERE p.Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<Profile> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var validation = Profile.ValidateName(name);
        if (validation is not null)
        {
            throw new ArgumentException(validation, nameof(name));
        }

        var trimmed = name.Trim();

        if (await NameExistsAsync(trimmed, cancellationToken))
        {
            throw new InvalidOperationException($"'{trimmed}' naam e already ekta profile ache.");
        }

        var created = DateTimeOffset.UtcNow;

        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Profiles (Name, CreatedUtc) VALUES ($name, $created);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$name", trimmed);
        command.Parameters.AddWithValue("$created", created.ToString("o", CultureInfo.InvariantCulture));

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return new Profile(id, trimmed, created, AppCount: 0);
    }

    public async Task<bool> RenameAsync(int id, string name, CancellationToken cancellationToken = default)
    {
        var validation = Profile.ValidateName(name);
        if (validation is not null)
        {
            throw new ArgumentException(validation, nameof(name));
        }

        var trimmed = name.Trim();

        if (await NameExistsAsync(trimmed, cancellationToken, excludeId: id))
        {
            throw new InvalidOperationException($"'{trimmed}' naam e already ekta profile ache.");
        }

        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Profiles SET Name = $name WHERE Id = $id;";
        command.Parameters.AddWithValue("$name", trimmed);
        command.Parameters.AddWithValue("$id", id);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Profiles WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        var deleted = await command.ExecuteNonQueryAsync(cancellationToken) > 0;

        if (deleted && await GetActiveProfileIdAsync(cancellationToken) == id)
        {
            await SetActiveProfileIdAsync(Profile.AllAppsProfileId, cancellationToken);
        }

        return deleted;
    }

    public async Task<IReadOnlyList<int>> GetAppIdsAsync(int profileId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ProtectedAppId FROM ProfileApps WHERE ProfileId = $id ORDER BY ProtectedAppId;";
        command.Parameters.AddWithValue("$id", profileId);

        var ids = new List<int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetInt32(0));
        }

        return ids;
    }

    public async Task SetAppsAsync(int profileId, IEnumerable<int> appIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appIds);

        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)
            await connection.BeginTransactionAsync(cancellationToken);

        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM ProfileApps WHERE ProfileId = $id;";
            clear.Parameters.AddWithValue("$id", profileId);
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var appId in appIds.Distinct())
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT OR IGNORE INTO ProfileApps (ProfileId, ProtectedAppId) VALUES ($profile, $app);";
            insert.Parameters.AddWithValue("$profile", profileId);
            insert.Parameters.AddWithValue("$app", appId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> GetActiveProfileIdAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _settings.GetIntAsync(
            SettingKeys.ActiveProfileId, Profile.AllAppsProfileId, cancellationToken);

        if (stored == Profile.AllAppsProfileId)
        {
            return Profile.AllAppsProfileId;
        }

        // Profile delete hoye gele stale id ke ignore koro.
        return await GetByIdAsync(stored, cancellationToken) is null
            ? Profile.AllAppsProfileId
            : stored;
    }

    public async Task SetActiveProfileIdAsync(int profileId, CancellationToken cancellationToken = default)
    {
        if (profileId != Profile.AllAppsProfileId && await GetByIdAsync(profileId, cancellationToken) is null)
        {
            throw new InvalidOperationException("Selected profile ar nei.");
        }

        await _settings.SetIntAsync(SettingKeys.ActiveProfileId, profileId, cancellationToken);
    }

    public async Task<IReadOnlySet<int>?> GetActiveAppIdsAsync(CancellationToken cancellationToken = default)
    {
        var profileId = await GetActiveProfileIdAsync(cancellationToken);

        if (profileId == Profile.AllAppsProfileId)
        {
            return null;
        }

        var ids = await GetAppIdsAsync(profileId, cancellationToken);
        return ids.ToHashSet();
    }

    private async Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken, int excludeId = 0)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(1) FROM Profiles WHERE Name = $name COLLATE NOCASE AND Id <> $exclude;";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$exclude", excludeId);

        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static Profile Map(Microsoft.Data.Sqlite.SqliteDataReader reader) => new(
        reader.GetInt32(0),
        reader.GetString(1),
        DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        reader.GetInt32(3));
}
