using System.Diagnostics;
using System.IO;
using Serilog;

namespace Dwarpal.App.Services;

/// <summary>
/// Windows Task Scheduler e "At logon, highest privileges" task. schtasks.exe use kora hoy,
/// karon app always admin chole ar task ta interactive user session ei start hoy.
/// </summary>
public static class AutoStartService
{
    public const string TaskName = "Dwarpal";

    public static bool IsEnabled()
    {
        var (exitCode, _) = Run("/Query", "/TN", TaskName);
        return exitCode == 0;
    }

    public static (bool Success, string Message) Enable()
    {
        var executablePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;

        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return (false, "Dwarpal.exe er path ber korte parlam na.");
        }

        var result = Run(
            "/Create",
            "/TN", TaskName,
            // schtasks parses /TR itself, so the executable path must be quoted
            // when it contains spaces (for example Program Files).
            "/TR", $"\"{executablePath}\"",
            "/SC", "ONLOGON",
            "/RL", "HIGHEST",
            "/F");

        return (result.ExitCode == 0, result.Output);
    }

    public static (bool Success, string Message) Disable()
    {
        var result = Run("/Delete", "/TN", TaskName, "/F");
        return (result.ExitCode == 0, result.Output);
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
            Log.Warning(ex, "schtasks call failed");
            return (-1, ex.Message);
        }
    }
}
