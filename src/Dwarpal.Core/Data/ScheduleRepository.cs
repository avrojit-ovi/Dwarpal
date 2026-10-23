using System.Globalization;
using Dwarpal.Core.Models;

namespace Dwarpal.Core.Data;

public sealed class ScheduleRepository
{
    private const string Columns = "Id, Name, DaysMask, StartMinutes, EndMinutes, IsEnabled, CreatedUtc";

    private readonly DwarpalDatabase _database;

    public ScheduleRepository(DwarpalDatabase database) => _database = database;

    public async Task<IReadOnlyList<ProtectionSchedule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM Schedules ORDER BY StartMinutes, Name COLLATE NOCASE;";

        var schedules = new List<ProtectionSchedule>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            schedules.Add(Map(reader));
        }

        return schedules;
    }

    public async Task<ProtectionSchedule> AddAsync(
        string name,
        int daysMask,
        int startMinutes,
        int endMinutes,
        bool isEnabled = true,
        CancellationToken cancellationToken = default)
    {
        var validation = ProtectionSchedule.Validate(name, daysMask, startMinutes, endMinutes);
        if (validation is not null)
        {
            throw new ArgumentException(validation, nameof(name));
        }

        var created = DateTimeOffset.UtcNow;

        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Schedules (Name, DaysMask, StartMinutes, EndMinutes, IsEnabled, CreatedUtc)
            VALUES ($name, $days, $start, $end, $enabled, $created);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$name", name.Trim());
        command.Parameters.AddWithValue("$days", daysMask);
        command.Parameters.AddWithValue("$start", startMinutes);
        command.Parameters.AddWithValue("$end", endMinutes);
        command.Parameters.AddWithValue("$enabled", isEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$created", created.ToString("o", CultureInfo.InvariantCulture));

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return new ProtectionSchedule(id, name.Trim(), daysMask, startMinutes, endMinutes, isEnabled, created);
    }

    public async Task<bool> SetEnabledAsync(int id, bool enabled, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Schedules SET IsEnabled = $enabled WHERE Id = $id;";
        command.Parameters.AddWithValue("$enabled", enabled ? 1 : 0);
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Schedules WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static ProtectionSchedule Map(Microsoft.Data.Sqlite.SqliteDataReader reader) => new(
        reader.GetInt32(0),
        reader.GetString(1),
        reader.GetInt32(2),
        reader.GetInt32(3),
        reader.GetInt32(4),
        reader.GetInt32(5) != 0,
        DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
}
