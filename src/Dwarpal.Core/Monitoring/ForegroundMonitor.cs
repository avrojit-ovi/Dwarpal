using System.Runtime.InteropServices;

namespace Dwarpal.Core.Monitoring;

/// <summary>
/// SetWinEventHook diye foreground change ar minimize start event.
/// WINEVENT_OUTOFCONTEXT mane callback installer thread er message loop e ashe (UI thread).
/// </summary>
public sealed class ForegroundMonitor : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventSystemMinimizeStart = 0x0016;
    private const uint WineventOutOfContext = 0x0000;
    private const uint WineventSkipOwnProcess = 0x0002;

    private readonly WinEventDelegate _callback;
    private IntPtr _foregroundHook;
    private IntPtr _minimizeHook;
    private bool _disposed;

    public ForegroundMonitor()
    {
        // Callback delegate ke field e rakha hoy - GC hole native callback crash korbe.
        _callback = OnWinEvent;
    }

    /// <summary>Foreground window jei process er, sei pid.</summary>
    public event EventHandler<int>? ForegroundProcessChanged;

    /// <summary>Kono window minimize hote start korle sei process er pid.</summary>
    public event EventHandler<int>? MinimizeStarted;

    public bool IsRunning => _foregroundHook != IntPtr.Zero || _minimizeHook != IntPtr.Zero;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRunning)
        {
            return;
        }

        _foregroundHook = SetWinEventHook(
            EventSystemForeground, EventSystemForeground, IntPtr.Zero, _callback, 0, 0,
            WineventOutOfContext | WineventSkipOwnProcess);

        _minimizeHook = SetWinEventHook(
            EventSystemMinimizeStart, EventSystemMinimizeStart, IntPtr.Zero, _callback, 0, 0,
            WineventOutOfContext | WineventSkipOwnProcess);
    }

    private void OnWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            GetWindowThreadProcessId(hwnd, out var processId);
            if (processId == 0)
            {
                return;
            }

            switch (eventType)
            {
                case EventSystemForeground:
                    ForegroundProcessChanged?.Invoke(this, (int)processId);
                    break;

                case EventSystemMinimizeStart:
                    MinimizeStarted?.Invoke(this, (int)processId);
                    break;
            }
        }
        catch (Exception)
        {
            // Native callback theke exception berono jabe na.
        }
    }

    public void Stop()
    {
        if (_foregroundHook != IntPtr.Zero)
        {
            UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
        }

        if (_minimizeHook != IntPtr.Zero)
        {
            UnhookWinEvent(_minimizeHook);
            _minimizeHook = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
