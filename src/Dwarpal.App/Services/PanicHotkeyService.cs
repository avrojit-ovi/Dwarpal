using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Serilog;

namespace Dwarpal.App.Services;

/// <summary>
/// Global panic hotkey (Ctrl+Alt+L). Message-only HwndSource use kore, tai dashboard
/// hidden thakleo kaj kore. Register fail korle app chalu thake - shudhu status e dekha jay.
/// </summary>
public sealed class PanicHotkeyService : IDisposable
{
    public const string DisplayChord = "Ctrl + Alt + L";

    private const int HotkeyId = 0x4457; // "DW"
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VirtualKeyL = 0x4C;
    private const int WmHotkey = 0x0312;
    private static readonly IntPtr HwndMessage = new(-3);

    private HwndSource? _source;
    private bool _disposed;

    public bool IsRegistered { get; private set; }
    public string? Error { get; private set; }

    public event EventHandler? Pressed;

    public void Register()
    {
        if (_disposed || _source is not null)
        {
            return;
        }

        try
        {
            var parameters = new HwndSourceParameters("DwarpalPanicHotkey")
            {
                ParentWindow = HwndMessage,
                Width = 0,
                Height = 0,
            };

            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);

            IsRegistered = RegisterHotKey(
                _source.Handle,
                HotkeyId,
                ModControl | ModAlt | ModNoRepeat,
                VirtualKeyL);

            if (IsRegistered)
            {
                Error = null;
                Log.Information("Panic hotkey registered: {Chord}", DisplayChord);
            }
            else
            {
                Error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                Log.Warning("Panic hotkey registration failed: {Error}", Error);
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Log.Warning(ex, "Panic hotkey setup failed");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_source is not null)
        {
            if (IsRegistered)
            {
                UnregisterHotKey(_source.Handle, HotkeyId);
            }

            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }

        IsRegistered = false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
