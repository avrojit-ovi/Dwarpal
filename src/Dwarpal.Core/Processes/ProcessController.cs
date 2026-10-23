using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;

namespace Dwarpal.Core.Processes;

/// <summary>
/// NtSuspendProcess / NtResumeProcess wrapper. Ei API documented na, kintu Windows 7 theke
/// stably kaj kore ar process ke pause kore dey (CPU + UI thread duitai theme jay).
/// </summary>
public static class ProcessController
{
    private const uint ProcessSuspendResume = 0x0800;

    [DllImport("ntdll.dll")]
    private static extern int NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll")]
    private static extern int NtResumeProcess(IntPtr processHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    public static bool TrySuspend(int processId, out string? error)
        => ChangeState(processId, suspend: true, out error);

    public static bool TryResume(int processId, out string? error)
        => ChangeState(processId, suspend: false, out error);

    public static bool IsRunning(int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Ei process er kono window ekhon foreground e ache kina.</summary>
    public static bool IsForeground(int processId)
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(window, out var foregroundProcessId);
        return foregroundProcessId == (uint)processId;
    }

    public static string? TryGetExecutablePath(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.MainModule?.FileName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return null;
        }
    }

    public static string? TryGetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName + ".exe";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static bool ChangeState(int processId, bool suspend, out string? error)
    {
        error = null;

        if (processId <= 0)
        {
            error = "Invalid process id.";
            return false;
        }

        var handle = OpenProcess(ProcessSuspendResume, inheritHandle: false, processId);
        if (handle == IntPtr.Zero)
        {
            error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return false;
        }

        try
        {
            var status = suspend ? NtSuspendProcess(handle) : NtResumeProcess(handle);

            if (status != 0)
            {
                error = $"NTSTATUS 0x{status:X8}";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    public static void TryResumeSilently(int processId)
    {
        if (!IsRunning(processId))
        {
            return;
        }

        if (!TryResume(processId, out var error))
        {
            Log.Warning("Could not resume process {ProcessId}: {Error}", processId, error);
        }
    }
}
