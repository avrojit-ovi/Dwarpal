using Dwarpal.Core.Models;
using Dwarpal.Core.Security;

namespace Dwarpal.Core.Data;

/// <summary>
/// Credential table (single row, Id=1). Hash gulo DB te plain base64 hisebe rakha hoy na -
/// DPAPI diye encrypt kora hoy, tai DB file churi korleo offline brute-force korte machine key lagbe.
/// </summary>
public sealed class CredentialRepository
{
    private readonly DwarpalDatabase _database;
    private readonly DpapiProtector _protector;

    public CredentialRepository(DwarpalDatabase database, DpapiProtector protector)
    {
        _database = database;
        _protector = protector;
    }

    public async Task<CredentialRecord?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Kind, SecretHash, RecoveryHash, CreatedUtc, UpdatedUtc FROM Credential WHERE Id = 1;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        if (!Enum.TryParse<CredentialKind>(reader.GetString(0), ignoreCase: true, out var kind))
        {
            throw new InvalidDataException("Stored credential kind is not recognized.");
        }

        return new CredentialRecord(
            kind,
            _protector.Unprotect(reader.GetString(1)),
            _protector.Unprotect(reader.GetString(2)),
            DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind),
            DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind));
    }

    public async Task SaveAsync(CredentialRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Credential (Id, Kind, SecretHash, RecoveryHash, CreatedUtc, UpdatedUtc)
            VALUES (1, $kind, $secretHash, $recoveryHash, $created, $updated)
            ON CONFLICT(Id) DO UPDATE SET
                Kind = excluded.Kind,
                SecretHash = excluded.SecretHash,
                RecoveryHash = excluded.RecoveryHash,
                UpdatedUtc = excluded.UpdatedUtc;
            """;

        command.Parameters.AddWithValue("$kind", record.Kind.ToString());
        command.Parameters.AddWithValue("$secretHash", _protector.Protect(record.SecretHash));
        command.Parameters.AddWithValue("$recoveryHash", _protector.Protect(record.RecoveryHash));
        command.Parameters.AddWithValue("$created", record.CreatedUtc.ToUniversalTime().ToString("o"));
        command.Parameters.AddWithValue("$updated", record.UpdatedUtc.ToUniversalTime().ToString("o"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM Credential WHERE Id = 1;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }
}
