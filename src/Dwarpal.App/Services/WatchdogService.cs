using System.Diagnostics;
using System.IO;
using Serilog;

namespace Dwarpal.App.Services;

/// <summary>
/// Tamper watchdog. Windows service session 0 theke interactive UI start korte pare na,
/// tai ei watchdog Task Scheduler e prottek minute chole:
/// mutex na pele main Dwarpal instance start kore, tarpor nijei exit kore.
/// </summary>
public static class WatchdogService
{
    public const string TaskName = "DwarpalWatchdog";

    public static string BuildTaskAction(string executablePath)
        => $"\"{executablePath}\" --watchdog";

    public static bool IsEnabled() => Run("/Query", "/TN", TaskName).ExitCode == 0;

    public static (bool Success, string Message) Enable()
    {
        var executablePath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return (false, "Dwarpal.exe er path ber korte parlam na.");
        }

        var result = Run(
            "/Create",
            "/TN", TaskName,
            "/TR", BuildTaskAction(executablePath),
            "/SC", "MINUTE",
            "/MO", "1",
            "/RL", "HIGHEST",
            "/IT",
            "/F");

        return (result.ExitCode == 0, result.Output);
    }

    public static (bool Success, string Message) Disable()
    {
        if (!IsEnabled())
        {
            return (true, "Watchdog already off.");
        }

        var result = Run("/Delete", "/TN", TaskName, "/F");
        return (result.ExitCode == 0, result.Output);
    }

    /// <summary>App start e setting on thakle task missing hole abar create kore.</summary>
    public static void EnsureEnabled()
    {
        if (IsEnabled())
        {
            return;
        }

        var result = Enable();
        if (!result.Success)
        {
            Log.Warning("Could not recreate watchdog task: {Message}", result.Message);
        }
    }

    /// <summary>`--watchdog` mode: main instance na thakle start kore, tarpor exit.</summary>
    public static int RunWatchdog()
    {
        if (SingleInstance.IsMainInstanceRunning())
        {
            return 0;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            Log.Warning("Watchdog could not resolve the Dwarpal executable path");
            return 1;
        }

        try
        {
            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
            };

            using var process = Process.Start(startInfo);
            Log.Information("Watchdog started a new Dwarpal instance (pid {ProcessId})", process?.Id);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Watchdog could not start Dwarpal");
            return 1;
        }
    }

    private static (int ExitCode, string Output) Run(params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo("schtasks.exe")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return (-1, "schtasks start korte parlam na.");
            }

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(15000);

            return (process.HasExited ? process.ExitCode : -1, output.Trim());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "schtasks watchdog call failed");
            return (-1, ex.Message);
        }
    }
}
