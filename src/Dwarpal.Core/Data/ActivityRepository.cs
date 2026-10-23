using System.Globalization;
using Dwarpal.Core.Models;

namespace Dwarpal.Core.Data;

public sealed class ActivityRepository
{
    private const string Columns = "Id, TimestampUtc, EventType, Outcome, AppName, AppPath, Detail, SelfiePath";

    private readonly DwarpalDatabase _database;

    public ActivityRepository(DwarpalDatabase database) => _database = database;

    public async Task<long> AddAsync(ActivityEntryDraft draft, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ActivityLog (TimestampUtc, EventType, Outcome, AppName, AppPath, Detail, SelfiePath)
            VALUES ($timestamp, $eventType, $outcome, $appName, $appPath, $detail, $selfie);
            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$timestamp", draft.TimestampUtc.ToUniversalTime().ToString("o"));
        command.Parameters.AddWithValue("$eventType", draft.EventType.ToString());
        command.Parameters.AddWithValue("$outcome", draft.Outcome.ToString());
        command.Parameters.AddWithValue("$appName", (object?)draft.AppName ?? DBNull.Value);
        command.Parameters.AddWithValue("$appPath", (object?)draft.AppPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$detail", (object?)draft.Detail ?? DBNull.Value);
        command.Parameters.AddWithValue("$selfie", (object?)draft.SelfiePath ?? DBNull.Value);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    public async Task<IReadOnlyList<ActivityEntry>> QueryAsync(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        string? search = null,
        int limit = 500,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var where = new List<string>();

        if (fromUtc is not null)
        {
            where.Add("TimestampUtc >= $from");
            command.Parameters.AddWithValue("$from", fromUtc.Value.ToUniversalTime().ToString("o"));
        }

        if (toUtc is not null)
        {
            where.Add("TimestampUtc <= $to");
            command.Parameters.AddWithValue("$to", toUtc.Value.ToUniversalTime().ToString("o"));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("(IFNULL(AppName, '') LIKE $search OR IFNULL(Detail, '') LIKE $search OR EventType LIKE $search)");
            command.Parameters.AddWithValue("$search", $"%{search.Trim()}%");
        }

        var whereClause = where.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", where);
        command.CommandText = $"SELECT {Columns} FROM ActivityLog {whereClause} ORDER BY Id DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5000));

        var entries = new List<ActivityEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new ActivityEntry(
                reader.GetInt64(0),
                DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Enum.Parse<ActivityEventType>(reader.GetString(2)),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                Enum.Parse<ActivityOutcome>(reader.GetString(3)),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return entries;
    }

    public async Task<int> CountAsync(
        ActivityEventType eventType,
        ActivityOutcome outcome,
        DateTimeOffset? fromUtc = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = fromUtc is null
            ? "SELECT COUNT(1) FROM ActivityLog WHERE EventType = $eventType AND Outcome = $outcome;"
            : "SELECT COUNT(1) FROM ActivityLog WHERE EventType = $eventType AND Outcome = $outcome AND TimestampUtc >= $from;";

        command.Parameters.AddWithValue("$eventType", eventType.ToString());
        command.Parameters.AddWithValue("$outcome", outcome.ToString());

        if (fromUtc is not null)
        {
            command.Parameters.AddWithValue("$from", fromUtc.Value.ToUniversalTime().ToString("o"));
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ActivityLog;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
