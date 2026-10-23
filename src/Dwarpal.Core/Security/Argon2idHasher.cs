using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Dwarpal.Core.Security;

public interface ISecretHasher
{
    string Hash(string secret);

    bool Verify(string secret, string encodedHash);

    bool NeedsRehash(string encodedHash);
}

/// <summary>
/// Argon2id memory-hard hashing. Encoded format:
/// $argon2id$v=19$m=65536,t=3,p=4$&lt;salt-b64&gt;$&lt;hash-b64&gt;
/// Salt ar parameters hash er sathe e thake, tai future e cost barale purano hash verify kora jabe.
/// </summary>
public sealed class Argon2idHasher : ISecretHasher
{
    public const int SaltSize = 16;
    public const int HashSize = 32;
    public const int DefaultMemoryKb = 65536; // 64 MB
    public const int DefaultIterations = 3;
    public const int DefaultParallelism = 4;

    private const string Algorithm = "argon2id";
    private const int CurrentVersion = 19;

    private readonly int _memoryKb;
    private readonly int _iterations;
    private readonly int _parallelism;

    public Argon2idHasher(
        int memoryKb = DefaultMemoryKb,
        int iterations = DefaultIterations,
        int parallelism = DefaultParallelism)
    {
        if (memoryKb < 8192) throw new ArgumentOutOfRangeException(nameof(memoryKb));
        if (iterations < 1) throw new ArgumentOutOfRangeException(nameof(iterations));
        if (parallelism < 1) throw new ArgumentOutOfRangeException(nameof(parallelism));

        _memoryKb = memoryKb;
        _iterations = iterations;
        _parallelism = parallelism;
    }

    public string Hash(string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(secret, salt, _memoryKb, _iterations, _parallelism);

        return string.Join('$',
            string.Empty,
            Algorithm,
            $"v={CurrentVersion}",
            $"m={_memoryKb},t={_iterations},p={_parallelism}",
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool Verify(string secret, string encodedHash)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrWhiteSpace(encodedHash))
        {
            return false;
        }

        if (!TryParse(encodedHash, out var parsed))
        {
            return false;
        }

        var computed = Derive(secret, parsed.Salt, parsed.MemoryKb, parsed.Iterations, parsed.Parallelism);

        // Timing attack thekanor jonno fixed-time compare.
        return CryptographicOperations.FixedTimeEquals(computed, parsed.Hash);
    }

    public bool NeedsRehash(string encodedHash)
    {
        if (!TryParse(encodedHash, out var parsed))
        {
            return true;
        }

        return parsed.MemoryKb != _memoryKb
               || parsed.Iterations != _iterations
               || parsed.Parallelism != _parallelism
               || parsed.Version != CurrentVersion;
    }

    private static byte[] Derive(string secret, byte[] salt, int memoryKb, int iterations, int parallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(secret))
        {
            Salt = salt,
            MemorySize = memoryKb,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };

        return argon2.GetBytes(HashSize);
    }

    private static bool TryParse(string encoded, out ParsedHash parsed)
    {
        parsed = default;

        // Leading '$' er karone first element empty string.
        var parts = encoded.Split('$');
        if (parts.Length != 6 || parts[0].Length != 0 || !string.Equals(parts[1], Algorithm, StringComparison.Ordinal))
        {
            return false;
        }

        if (!parts[2].StartsWith("v=", StringComparison.Ordinal)
            || !int.TryParse(parts[2].AsSpan(2), out var version))
        {
            return false;
        }

        var parameters = parts[3].Split(',');
        if (parameters.Length != 3)
        {
            return false;
        }

        if (!TryReadParameter(parameters[0], "m=", out var memoryKb)
            || !TryReadParameter(parameters[1], "t=", out var iterations)
            || !TryReadParameter(parameters[2], "p=", out var parallelism))
        {
            return false;
        }

        if (memoryKb < 1024 || iterations < 1 || parallelism < 1)
        {
            return false;
        }

        byte[] salt;
        byte[] hash;
        try
        {
            salt = Convert.FromBase64String(parts[4]);
            hash = Convert.FromBase64String(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length < SaltSize || hash.Length != HashSize)
        {
            return false;
        }

        parsed = new ParsedHash(version, salt, hash, memoryKb, iterations, parallelism);
        return true;
    }

    private static bool TryReadParameter(string value, string prefix, out int result)
    {
        result = 0;
        return value.StartsWith(prefix, StringComparison.Ordinal)
               && int.TryParse(value.AsSpan(prefix.Length), out result);
    }

    private readonly record struct ParsedHash(
        int Version,
        byte[] Salt,
        byte[] Hash,
        int MemoryKb,
        int Iterations,
        int Parallelism);
}
