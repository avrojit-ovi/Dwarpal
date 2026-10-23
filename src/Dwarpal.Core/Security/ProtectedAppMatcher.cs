using Dwarpal.Core.Models;

namespace Dwarpal.Core.Security;

/// <summary>
/// Process ki protected list er kono app er sathe match kore kina.
/// 1) Path exact match (case-insensitive) - normal case.
/// 2) Process name match - path ber korte na parleo (system process, access denied) lock hobe,
///    ar same name e copy/rename kore bypass korleo dhora porbe.
/// </summary>
public static class ProtectedAppMatcher
{
    public static bool Matches(ProtectedApp app, string? processName, string? executablePath)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.IsEnabled)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            try
            {
                var actualPath = Path.GetFullPath(executablePath);
                if (string.Equals(actualPath, app.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Invalid path ashle name match e fallback.
            }
        }

        if (!string.IsNullOrWhiteSpace(processName))
        {
            var actualName = Path.GetFileNameWithoutExtension(processName);

            if (string.Equals(actualName, app.ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
