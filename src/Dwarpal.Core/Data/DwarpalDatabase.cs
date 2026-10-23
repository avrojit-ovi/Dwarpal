using Microsoft.Data.Sqlite;

namespace Dwarpal.Core.Data;

/// <summary>
/// SQLite connection factory + schema bootstrap.
/// Migration logic: PRAGMA user_version ekhon koto version, tar upor base kore script apply hoy.
/// </summary>
public sealed class DwarpalDatabase
{
    public const int CurrentSchemaVersion = 4;

    private readonly string _databasePath;
    private readonly string _connectionString;

    public DwarpalDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        _databasePath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
        }.ToString();
    }

    public string DatabasePath => _databasePath;

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Connection pool reuse korleo foreign key per-connection enable korte hoy.
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
            await pragma.ExecuteNonQueryAsync(cancellationToken);
        }

        return connection;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = await OpenAsync(cancellationToken);

        // WAL transaction er bhitore set kora jay na.
        await using (var wal = connection.CreateCommand())
        {
            wal.CommandText = "PRAGMA journal_mode = WAL;";
            await wal.ExecuteNonQueryAsync(cancellationToken);
        }

        var version = await GetUserVersionAsync(connection, cancellationToken);
        if (version >= CurrentSchemaVersion)
        {
            return;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        if (version < 1)
        {
            await ExecuteScriptAsync(connection, transaction, SchemaV1, cancellationToken);
        }

        if (version < 2)
        {
            await ExecuteScriptAsync(connection, transaction, SchemaV2, cancellationToken);
        }

        if (version < 3)
        {
            await ExecuteScriptAsync(connection, transaction, SchemaV3, cancellationToken);
        }

        if (version < 4)
        {
            await ExecuteScriptAsync(connection, transaction, SchemaV4, cancellationToken);
        }

        await using (var bump = connection.CreateCommand())
        {
            bump.Transaction = transaction;
            // PRAGMA e parameter bind kora jay na; value compile-time constant (safe).
            bump.CommandText = $"PRAGMA user_version = {CurrentSchemaVersion};";
            await bump.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ExecuteScriptAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string script,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = script;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> GetUserVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private const string SchemaV1 = """
        CREATE TABLE IF NOT EXISTS Settings (
            Key         TEXT NOT NULL PRIMARY KEY,
            Value       TEXT NOT NULL,
            UpdatedUtc  TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Credential (
            Id          INTEGER NOT NULL PRIMARY KEY CHECK (Id = 1),
            Kind        TEXT NOT NULL,
            SecretHash  TEXT NOT NULL,
            RecoveryHash TEXT NOT NULL,
            CreatedUtc  TEXT NOT NULL,
            UpdatedUtc  TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS ActivityLog (
            Id          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            TimestampUtc TEXT NOT NULL,
            EventType   TEXT NOT NULL,
            Outcome     TEXT NOT NULL,
            AppName     TEXT NULL,
            AppPath     TEXT NULL,
            Detail      TEXT NULL,
            SelfiePath  TEXT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_ActivityLog_TimestampUtc ON ActivityLog (TimestampUtc);
        CREATE INDEX IF NOT EXISTS IX_ActivityLog_EventType ON ActivityLog (EventType);
        """;

    private const string SchemaV2 = """
        CREATE TABLE IF NOT EXISTS ProtectedApps (
            Id           INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            DisplayName  TEXT NOT NULL,
            ExecutablePath TEXT NOT NULL,
            ProcessName  TEXT NOT NULL,
            IsEnabled    INTEGER NOT NULL DEFAULT 1,
            CreatedUtc   TEXT NOT NULL,
            LastLockedUtc TEXT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS IX_ProtectedApps_ExecutablePath
            ON ProtectedApps (ExecutablePath COLLATE NOCASE);

        CREATE INDEX IF NOT EXISTS IX_ProtectedApps_ProcessName
            ON ProtectedApps (ProcessName COLLATE NOCASE);
        """;

    private const string SchemaV3 = """
        ALTER TABLE ProtectedApps ADD COLUMN AutoRelockMinutes INTEGER NULL;
        """;

    private const string SchemaV4 = """
        CREATE TABLE IF NOT EXISTS Profiles (
            Id          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Name        TEXT NOT NULL,
            CreatedUtc  TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS IX_Profiles_Name ON Profiles (Name COLLATE NOCASE);

        CREATE TABLE IF NOT EXISTS ProfileApps (
            ProfileId      INTEGER NOT NULL REFERENCES Profiles (Id) ON DELETE CASCADE,
            ProtectedAppId INTEGER NOT NULL REFERENCES ProtectedApps (Id) ON DELETE CASCADE,
            PRIMARY KEY (ProfileId, ProtectedAppId)
        );

        CREATE TABLE IF NOT EXISTS Schedules (
            Id            INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Name          TEXT NOT NULL,
            DaysMask      INTEGER NOT NULL,
            StartMinutes  INTEGER NOT NULL,
            EndMinutes    INTEGER NOT NULL,
            IsEnabled     INTEGER NOT NULL DEFAULT 1,
            CreatedUtc    TEXT NOT NULL
        );
        """;
}
