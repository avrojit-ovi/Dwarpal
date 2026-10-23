using Microsoft.Win32;
using Serilog;

namespace Dwarpal.Core.Applications;

/// <summary>
/// Installed application list doita source theke: Registry Uninstall keys ar Start Menu shortcuts.
/// Duplicate (same exe path) bad jay, ar jekono ekta source fail korleo baki ta kaj kore.
/// </summary>
public sealed class InstalledAppScanner
{
    private static readonly string[] UninstallKeys =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    };

    private static readonly string[] SkippedNameFragments =
    {
        "uninstall", "readme", "documentation", "release notes", "update for",
    };

    /// <summary>
    /// Scan dedicated STA thread e chole - Start Menu .lnk resolve korte COM (WScript.Shell) lagbe,
    /// ar COM STA apartment chay. Registry scan ei jonyo block hoy na (alada thread).
    /// </summary>
    public Task<IReadOnlyList<InstalledApp>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<IReadOnlyList<InstalledApp>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                completion.TrySetResult(Scan(cancellationToken));
            }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "DwarpalAppScan",
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return completion.Task;
    }

    private static IReadOnlyList<InstalledApp> Scan(CancellationToken cancellationToken)
    {
        var apps = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

        try
        {
            ScanRegistry(apps, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Registry app scan failed");
        }

        try
        {
            ScanStartMenu(apps, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Start menu app scan failed");
        }

        return apps.Values
            .OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void ScanRegistry(Dictionary<string, InstalledApp> apps, CancellationToken cancellationToken)
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var uninstallPath in UninstallKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                    using var uninstall = baseKey.OpenSubKey(uninstallPath);
                    if (uninstall is null)
                    {
                        continue;
                    }

                    foreach (var subKeyName in uninstall.GetSubKeyNames())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            using var appKey = uninstall.OpenSubKey(subKeyName);
                            if (appKey is null)
                            {
                                continue;
                            }

                            var app = ReadRegistryEntry(appKey);
                            if (app is not null)
                            {
                                apps.TryAdd(app.ExecutablePath, app);
                            }
                        }
                        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                        {
                            // Ekta entry fail korle baki list continue korbe.
                        }
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    Log.Debug(ex, "Could not read uninstall key {Hive}/{Path}", hive, uninstallPath);
                }
            }
        }
    }

    private static InstalledApp? ReadRegistryEntry(RegistryKey appKey)
    {
        var displayName = appKey.GetValue("DisplayName") as string;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        if (Convert.ToInt32(appKey.GetValue("SystemComponent", 0)) == 1)
        {
            return null;
        }

        // Update/patch entry gulo app na.
        if (appKey.GetValue("ParentKeyName") is not null)
        {
            return null;
        }

        if (SkippedNameFragments.Any(fragment =>
                displayName.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var displayIcon = appKey.GetValue("DisplayIcon") as string;
        var installLocation = appKey.GetValue("InstallLocation") as string;

        var executable = ResolveExecutable(displayIcon, installLocation, displayName);
        if (executable is null)
        {
            return null;
        }

        var iconPath = ExtractIconPath(displayIcon) ?? executable;

        return new InstalledApp(
            displayName.Trim(),
            executable,
            File.Exists(iconPath) ? iconPath : executable,
            InstalledAppSource.Registry);
    }

    private static void ScanStartMenu(Dictionary<string, InstalledApp> apps, CancellationToken cancellationToken)
    {
        var startMenuRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        };

        foreach (var root in startMenuRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var programs = Path.Combine(root, "Programs");
            if (!Directory.Exists(programs))
            {
                continue;
            }

            IEnumerable<string> shortcuts;
            try
            {
                shortcuts = Directory.EnumerateFiles(programs, "*.lnk", SearchOption.AllDirectories);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var shortcut in shortcuts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var target = ShortcutResolver.TryResolveTarget(shortcut);
                if (string.IsNullOrWhiteSpace(target)
                    || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(target))
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(shortcut);
                if (string.IsNullOrWhiteSpace(name)
                    || SkippedNameFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                apps.TryAdd(target, new InstalledApp(
                    name,
                    target,
                    target,
                    InstalledAppSource.StartMenu));
            }
        }
    }

    private static string? ResolveExecutable(string? displayIcon, string? installLocation, string displayName)
    {
        var iconPath = ExtractIconPath(displayIcon);

        if (iconPath is not null
            && iconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            && File.Exists(iconPath))
        {
            return Path.GetFullPath(iconPath);
        }

        if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation))
        {
            return null;
        }

        // Common convention: install folder e display name er exe.
        var candidate = Path.Combine(installLocation, displayName + ".exe");
        return File.Exists(candidate) ? Path.GetFullPath(candidate) : null;
    }

    /// <summary>
    /// "C:\App\app.exe,0" ba "\"C:\App\app.exe\" --flag" theke sudhu exe/icon path ber kore.
    /// </summary>
    internal static string? ExtractIconPath(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
        {
            return null;
        }

        var value = Environment.ExpandEnvironmentVariables(displayIcon.Trim());

        if (value.StartsWith('"'))
        {
            var closing = value.IndexOf('"', 1);
            value = closing > 1 ? value[1..closing] : value.Trim('"');
        }
        else
        {
            var comma = value.LastIndexOf(',');
            if (comma > 2 && int.TryParse(value[(comma + 1)..].Trim(), out _))
            {
                value = value[..comma];
            }
        }

        value = value.Trim().Trim('"');
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
