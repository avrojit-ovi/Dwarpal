using System.Security.AccessControl;
using System.Security.Principal;
using Serilog;

namespace Dwarpal.Core;

/// <summary>
/// App er sob persistent file location ek jaygay rakha holo.
/// Default: %ProgramData%\Dwarpal (machine-wide, SYSTEM service ar elevated app duijon e access korte parbe).
/// </summary>
public sealed class AppPaths
{
    public const string ProductName = "Dwarpal";
    public const string DatabaseFileName = "dwarpal.db";

    private readonly bool _hardenAcl;

    public AppPaths(string? rootDirectory = null, bool hardenAcl = true)
    {
        RootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ProductName)
            : Path.GetFullPath(rootDirectory);

        _hardenAcl = hardenAcl;
    }

    public string RootDirectory { get; }
    public string DatabasePath => Path.Combine(RootDirectory, DatabaseFileName);
    public string LogDirectory => Path.Combine(RootDirectory, "logs");
    public string SelfieDirectory => Path.Combine(RootDirectory, "selfies");

    /// <summary>
    /// Folder gulo create kore ACL harden kore: sudhu SYSTEM + Administrators full control.
    /// Standard user credential hashes ba log chhunte parbe na.
    /// </summary>
    public void EnsureCreated()
    {
        // Prothome sob folder banao, tarpor ACL harden koro - ulta korle hardened root er
        // vitore child folder create korte giye access denied hote pare.
        var directories = new[] { RootDirectory, LogDirectory, SelfieDirectory };

        foreach (var directory in directories)
        {
            Directory.CreateDirectory(directory);
        }

        if (!_hardenAcl)
        {
            return;
        }

        foreach (var directory in directories)
        {
            TryHardenDirectoryAcl(directory);
        }
    }

    private static void TryHardenDirectoryAcl(string directory)
    {
        try
        {
            var info = new DirectoryInfo(directory);
            var security = new DirectorySecurity();

            // Default inherited ACE gulo bad diye nijer rule bosano hocche.
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

            security.AddAccessRule(new FileSystemAccessRule(
                system, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));

            security.AddAccessRule(new FileSystemAccessRule(
                administrators, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));

            info.SetAccessControl(security);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PrivilegeNotHeldException or PlatformNotSupportedException or IOException)
        {
            // ACL set korte na parle app bondho hobe na - shudhu warn kore jabe.
            Log.Warning(ex, "Could not harden ACL for {Directory}", directory);
        }
    }
}
