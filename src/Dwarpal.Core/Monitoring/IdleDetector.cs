using System.Runtime.InteropServices;

namespace Dwarpal.Core.Monitoring;

/// <summary>
/// System-wide user idle time (keyboard/mouse). Auto re-lock er "inactivity" part ei value use kore.
/// </summary>
public static class IdleDetector
{
    public static TimeSpan GetIdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };

        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        // TickCount 49.7 din por wrap hoy; unchecked subtraction eo difference thik thake.
        var idleMilliseconds = unchecked(Environment.TickCount - (int)info.Time);

        return idleMilliseconds <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(idleMilliseconds);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo lastInputInfo);
}
