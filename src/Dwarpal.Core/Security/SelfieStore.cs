using System.Security.Cryptography;
using System.Text;

namespace Dwarpal.Core.Security;

/// <summary>
/// Intruder selfie JPEG gulo DPAPI (LocalMachine) diye encrypt kore selfies folder e rakhe.
/// DB te shudhu path jay; file onno account/machine e khola jabe na.
/// </summary>
public sealed class SelfieStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Dwarpal.Selfie.v1");

    private readonly AppPaths _paths;

    public SelfieStore(AppPaths paths) => _paths = paths;

    public async Task<string> SaveAsync(byte[] jpeg, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jpeg);

        Directory.CreateDirectory(_paths.SelfieDirectory);

        var name = $"intruder-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.bin";
        var path = Path.Combine(_paths.SelfieDirectory, name);
        var encrypted = ProtectedData.Protect(jpeg, Entropy, DataProtectionScope.LocalMachine);

        await File.WriteAllBytesAsync(path, encrypted, cancellationToken);
        return path;
    }

    public byte[] Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = ResolveInsideSelfieDirectory(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Selfie file not found.", fullPath);
        }

        var encrypted = File.ReadAllBytes(fullPath);
        return ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.LocalMachine);
    }

    public bool Exists(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            return File.Exists(ResolveInsideSelfieDirectory(path));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private string ResolveInsideSelfieDirectory(string path)
    {
        var root = Path.GetFullPath(_paths.SelfieDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Selfie path is outside the selfie directory.", nameof(path));
        }

        return fullPath;
    }
}
