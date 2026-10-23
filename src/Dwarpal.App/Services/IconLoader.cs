using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;

namespace Dwarpal.App.Services;

/// <summary>
/// .exe/.ico theke small icon ber kore frozen BitmapSource hisebe cache kore.
/// Frozen image gulo UI thread charao safely use kora jay.
/// </summary>
public static class IconLoader
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? Load(string? path, int size = 32)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var iconPath = NormalizeIconPath(path);
        if (iconPath is null || iconPath.Length == 0 || !File.Exists(iconPath))
        {
            return null;
        }

        var resolvedPath = iconPath;
        var cacheKey = $"{size}|{resolvedPath}";
        return Cache.GetOrAdd(cacheKey, _ => Extract(resolvedPath, size));
    }

    private static ImageSource? Extract(string iconPath, int size)
    {
        try
        {
            using var icon = iconPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
                ? new Icon(iconPath, size, size)
                : Icon.ExtractAssociatedIcon(iconPath);

            if (icon is null)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(size, size));

            source.Freeze();
            return source;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Log.Debug(ex, "Icon extract failed for {Path}", iconPath);
            return null;
        }
    }

    /// <summary>Registry DisplayIcon e "app.exe,0" ba quote thakte pare.</summary>
    private static string? NormalizeIconPath(string path)
    {
        var value = Environment.ExpandEnvironmentVariables(path.Trim());
        value = value.Trim('"');

        var comma = value.LastIndexOf(',');
        if (comma > 2 && int.TryParse(value[(comma + 1)..].Trim(), out _))
        {
            value = value[..comma];
        }

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim('"');
    }
}
