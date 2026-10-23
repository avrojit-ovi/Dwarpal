using System.Runtime.InteropServices;

namespace Dwarpal.Core.Applications;

/// <summary>
/// .lnk shortcut resolve korte Windows er WScript.Shell COM use kora hoy.
/// COM unavailable hole null return kore - scanner baki source diye cholbe.
/// </summary>
public static class ShortcutResolver
{
    private static readonly object ComGate = new();

    public static string? TryResolveTarget(string shortcutPath)
    {
        if (!File.Exists(shortcutPath))
        {
            return null;
        }

        // COM apartment related race avoid korte serialize kora hoy.
        lock (ComGate)
        {
            object? shell = null;
            object? shortcut = null;

            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType is null)
                {
                    return null;
                }

                shell = Activator.CreateInstance(shellType);
                if (shell is null)
                {
                    return null;
                }

                dynamic dynamicShell = shell;
                shortcut = dynamicShell.CreateShortcut(shortcutPath);

                dynamic dynamicShortcut = shortcut;
                string? target = dynamicShortcut.TargetPath;

                return string.IsNullOrWhiteSpace(target) ? null : target;
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException or NotSupportedException)
            {
                return null;
            }
            finally
            {
                if (shortcut is not null && Marshal.IsComObject(shortcut))
                {
                    Marshal.FinalReleaseComObject(shortcut);
                }

                if (shell is not null && Marshal.IsComObject(shell))
                {
                    Marshal.FinalReleaseComObject(shell);
                }
            }
        }
    }
}
