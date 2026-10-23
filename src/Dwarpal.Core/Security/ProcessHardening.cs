using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Dwarpal.Core.Security;

/// <summary>
/// Process DACL hardening: "BUILTIN\Users" ke PROCESS_TERMINATE deny kore.
/// Result: non-elevated Task Manager / taskkill diye Dwarpal bondho kora jay na.
/// Ekjon local administrator DACL change kore bypass korte pare - eta deterrent, sandbox noy.
/// Test/debug er jonno DWARPAL_DISABLE_DACL=1 diye off kora jay.
/// </summary>
public sealed class ProcessAclSnapshot
{
    internal ProcessAclSnapshot(byte[] daclBytes) => DaclBytes = daclBytes;

    internal byte[] DaclBytes { get; }
}

public static class ProcessHardening
{
    public const string DisableEnvironmentVariable = "DWARPAL_DISABLE_DACL";

    private const uint ProcessTerminate = 0x0001;
    private const uint ReadControl = 0x00020000;
    private const uint WriteDac = 0x00040000;

    private const int SeKernelObject = 6;
    private const int DaclSecurityInformation = 0x00000004;

    private const int DenyAccess = 3;
    private const int NoInheritance = 0;
    private const int AclSizeInformation = 2;

    private const int TrusteeIsSid = 0;
    private const int TrusteeIsWellKnownGroup = 5;

    public static bool IsDisabledByEnvironment =>
        string.Equals(Environment.GetEnvironmentVariable(DisableEnvironmentVariable), "1", StringComparison.Ordinal);

    public static bool TryApplyToCurrentProcess(out string? error)
    {
        using var process = Process.GetCurrentProcess();
        return TryApply(process, out _, out error);
    }

    /// <summary>
    /// Ei process er DACL e Users group er jonno deny-terminate ACE add kore.
    /// Snapshot ta restore path er jonno - test cleanup e original DACL ferot deya jay.
    /// </summary>
    public static bool TryApply(Process process, out ProcessAclSnapshot? snapshot, out string? error)
    {
        ArgumentNullException.ThrowIfNull(process);
        snapshot = null;
        error = null;

        if (!OperatingSystem.IsWindows())
        {
            error = "DACL hardening is only supported on Windows.";
            return false;
        }

        return ChangeDacl(process.Id, out snapshot, out error);
    }

    /// <summary>Original DACL ferot dey (test/debug cleanup).</summary>
    public static bool TryRestore(Process process, ProcessAclSnapshot snapshot, out string? error)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(snapshot);
        error = null;

        var handle = OpenProcess(ReadControl | WriteDac, inheritHandle: false, process.Id);
        if (handle == IntPtr.Zero)
        {
            error = Win32("OpenProcess");
            return false;
        }

        var daclPointer = IntPtr.Zero;

        try
        {
            daclPointer = Marshal.AllocHGlobal(snapshot.DaclBytes.Length);
            Marshal.Copy(snapshot.DaclBytes, 0, daclPointer, snapshot.DaclBytes.Length);

            var result = SetSecurityInfo(
                handle, SeKernelObject, DaclSecurityInformation, IntPtr.Zero, IntPtr.Zero, daclPointer, IntPtr.Zero);

            if (result != 0)
            {
                error = Win32("SetSecurityInfo", result);
                return false;
            }

            return true;
        }
        finally
        {
            if (daclPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(daclPointer);
            }

            CloseHandle(handle);
        }
    }

    private static bool ChangeDacl(int processId, out ProcessAclSnapshot? snapshot, out string? error)
    {
        snapshot = null;
        error = null;
        var handle = OpenProcess(ReadControl | WriteDac, inheritHandle: false, processId);

        if (handle == IntPtr.Zero)
        {
            error = Win32("OpenProcess");
            return false;
        }

        var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var sidBytes = new byte[sid.BinaryLength];
        sid.GetBinaryForm(sidBytes, 0);

        var sidPointer = Marshal.AllocHGlobal(sidBytes.Length);
        var securityDescriptor = IntPtr.Zero;
        var newAcl = IntPtr.Zero;

        try
        {
            Marshal.Copy(sidBytes, 0, sidPointer, sidBytes.Length);

            var getResult = GetSecurityInfo(
                handle,
                SeKernelObject,
                DaclSecurityInformation,
                out _,
                out _,
                out var existingDacl,
                out _,
                out securityDescriptor);

            if (getResult != 0)
            {
                error = Win32("GetSecurityInfo", getResult);
                return false;
            }

            if (existingDacl == IntPtr.Zero)
            {
                error = "Process has a null DACL; refusing to replace it with a deny-only DACL.";
                return false;
            }

            if (GetAclInformation(
                    existingDacl,
                    out var aclInfo,
                    (uint)Marshal.SizeOf<AclSizeInformationStruct>(),
                    AclSizeInformation) == false)
            {
                error = Win32("GetAclInformation");
                return false;
            }

            var originalDacl = new byte[aclInfo.BytesInUse];
            Marshal.Copy(existingDacl, originalDacl, 0, originalDacl.Length);
            snapshot = new ProcessAclSnapshot(originalDacl);

            var entry = new ExplicitAccess
            {
                AccessPermissions = ProcessTerminate,
                AccessMode = DenyAccess,
                Inheritance = NoInheritance,
                Trustee = new Trustee
                {
                    MultipleTrustee = IntPtr.Zero,
                    MultipleTrusteeOperation = 0,
                    TrusteeForm = TrusteeIsSid,
                    TrusteeType = TrusteeIsWellKnownGroup,
                    Name = sidPointer,
                },
            };

            var aclResult = SetEntriesInAcl(1, ref entry, existingDacl, out newAcl);
            if (aclResult != 0)
            {
                error = Win32("SetEntriesInAcl", aclResult);
                return false;
            }

            var setResult = SetSecurityInfo(
                handle, SeKernelObject, DaclSecurityInformation, IntPtr.Zero, IntPtr.Zero, newAcl, IntPtr.Zero);

            if (setResult != 0)
            {
                error = Win32("SetSecurityInfo", setResult);
                return false;
            }

            return true;
        }
        finally
        {
            if (newAcl != IntPtr.Zero)
            {
                LocalFree(newAcl);
            }

            if (securityDescriptor != IntPtr.Zero)
            {
                LocalFree(securityDescriptor);
            }

            Marshal.FreeHGlobal(sidPointer);
            CloseHandle(handle);
        }
    }

    private static string Win32(string operation, int? code = null)
        => $"{operation} failed: {new Win32Exception(code ?? Marshal.GetLastWin32Error()).Message}";

    [StructLayout(LayoutKind.Sequential)]
    private struct Trustee
    {
        public IntPtr MultipleTrustee;
        public int MultipleTrusteeOperation;
        public int TrusteeForm;
        public int TrusteeType;
        public IntPtr Name;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AclSizeInformationStruct
    {
        public uint AceCount;
        public uint BytesInUse;
        public uint BytesFree;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExplicitAccess
    {
        public uint AccessPermissions;
        public int AccessMode;
        public int Inheritance;
        public Trustee Trustee;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int GetSecurityInfo(
        IntPtr handle,
        int objectType,
        int securityInfo,
        out IntPtr owner,
        out IntPtr group,
        out IntPtr dacl,
        out IntPtr sacl,
        out IntPtr securityDescriptor);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int SetSecurityInfo(
        IntPtr handle,
        int objectType,
        int securityInfo,
        IntPtr owner,
        IntPtr group,
        IntPtr dacl,
        IntPtr sacl);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetAclInformation(
        IntPtr acl,
        out AclSizeInformationStruct information,
        uint informationSize,
        int informationClass);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "SetEntriesInAclW")]
    private static extern int SetEntriesInAcl(
        uint count,
        ref ExplicitAccess explicitAccess,
        IntPtr oldAcl,
        out IntPtr newAcl);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
