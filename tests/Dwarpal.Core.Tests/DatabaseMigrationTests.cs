using Dwarpal.Core.Data;
using Microsoft.Data.Sqlite;

namespace Dwarpal.Core.Tests;

public class DatabaseMigrationTests
{
    [Fact]
    public async Task Initialize_UpgradesV1Database_WithoutLosingData()
    {
        var root = Path.Combine(Path.GetTempPath(), "dwarpal-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "dwarpal-v1.db");

        try
        {
            await CreateVersion1DatabaseAsync(databasePath);

            var database = new DwarpalDatabase(databasePath);
            await database.InitializeAsync();

            await using var connection = await database.OpenAsync();

            await using (var version = connection.CreateCommand())
            {
                version.CommandText = "PRAGMA user_version;";
                Assert.Equal(DwarpalDatabase.CurrentSchemaVersion, Convert.ToInt32(await version.ExecuteScalarAsync()));
            }

            await using (var settings = connection.CreateCommand())
            {
                settings.CommandText = "SELECT Value FROM Settings WHERE Key = 'phase1';";
                Assert.Equal("kept", await settings.ExecuteScalarAsync() as string);
            }

            await using (var apps = connection.CreateCommand())
            {
                apps.CommandText = "SELECT COUNT(1) FROM ProtectedApps;";
                Assert.Equal(0L, Convert.ToInt64(await apps.ExecuteScalarAsync()));
            }

            await using (var column = connection.CreateCommand())
            {
                column.CommandText = "SELECT COUNT(1) FROM pragma_table_info('ProtectedApps') WHERE name = 'AutoRelockMinutes';";
                Assert.Equal(1L, Convert.ToInt64(await column.ExecuteScalarAsync()));
            }

            await using (var tables = connection.CreateCommand())
            {
                tables.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name IN ('Profiles', 'ProfileApps', 'Schedules');";
                Assert.Equal(3L, Convert.ToInt64(await tables.ExecuteScalarAsync()));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    private static async Task CreateVersion1DatabaseAsync(string path)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Settings (
                Key TEXT NOT NULL PRIMARY KEY,
                Value TEXT NOT NULL,
                UpdatedUtc TEXT NOT NULL
            );
            CREATE TABLE Credential (
                Id INTEGER NOT NULL PRIMARY KEY CHECK (Id = 1),
                Kind TEXT NOT NULL,
                SecretHash TEXT NOT NULL,
                RecoveryHash TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL,
                UpdatedUtc TEXT NOT NULL
            );
            CREATE TABLE ActivityLog (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                TimestampUtc TEXT NOT NULL,
                EventType TEXT NOT NULL,
                Outcome TEXT NOT NULL,
                AppName TEXT NULL,
                AppPath TEXT NULL,
                Detail TEXT NULL,
                SelfiePath TEXT NULL
            );
            INSERT INTO Settings (Key, Value, UpdatedUtc) VALUES ('phase1', 'kept', '2026-01-01T00:00:00.0000000+00:00');
            PRAGMA user_version = 1;
            """;
        await command.ExecuteNonQueryAsync();
    }
}
