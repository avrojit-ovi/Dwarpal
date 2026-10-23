using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Serilog;

namespace Dwarpal.App.Services;

/// <summary>
/// Lock screen er blur background er jonno virtual screen er ekta snapshot.
/// Capture fail korle null - lock screen tokhon gradient background e cholbe.
/// </summary>
public static class ScreenSnapshot
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    public static BitmapSource? CaptureVirtualScreen()
    {
        try
        {
            var x = GetSystemMetrics(SmXVirtualScreen);
            var y = GetSystemMetrics(SmYVirtualScreen);
            var width = GetSystemMetrics(SmCxVirtualScreen);
            var height = GetSystemMetrics(SmCyVirtualScreen);

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(x, y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
            }

            var handle = bitmap.GetHbitmap();
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                // Full-size 4K image ke chhoto kore blur korle lock screen open fast hoy.
                var scaled = new TransformedBitmap(source, new System.Windows.Media.ScaleTransform(0.5, 0.5));
                scaled.Freeze();
                return scaled;
            }
            finally
            {
                DeleteObject(handle);
            }
        }
        catch (Exception ex) when (ex is ExternalException or ArgumentException or InvalidOperationException or OutOfMemoryException)
        {
            Log.Debug(ex, "Screen snapshot failed; lock screen will use gradient background");
            return null;
        }
    }
}
