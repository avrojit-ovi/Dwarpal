using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using Serilog;

namespace Dwarpal.App.Services;

/// <summary>
/// Dashboard close korle app exit na kore tray e chole jay - lock engine chalu thake.
/// Tray menu theke window abar khola jay; Exit credential chhara hobe na (App dekhe).
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private bool _disposed;
    private bool _hiddenTipShown;

    public TrayIconService(Action open, Action exit)
    {
        var menu = new Forms.ContextMenuStrip
        {
            ShowImageMargin = false,
            BackColor = Drawing.Color.FromArgb(12, 22, 38),
            ForeColor = Drawing.Color.FromArgb(242, 246, 252),
            Renderer = new Forms.ToolStripProfessionalRenderer(new DarkColorTable()),
        };

        var openItem = new Forms.ToolStripMenuItem("Open Dwarpal") { Font = new Drawing.Font("Segoe UI", 9.5f, Drawing.FontStyle.Bold) };
        openItem.Click += (_, _) => open();

        var exitItem = new Forms.ToolStripMenuItem("Exit Dwarpal…");
        exitItem.Click += (_, _) => exit();

        menu.Items.Add(openItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "Dwarpal — app guard",
            Icon = LoadIcon(),
            Visible = true,
            ContextMenuStrip = menu,
        };

        _notifyIcon.DoubleClick += (_, _) => open();
    }

    public void ShowHiddenNotification()
    {
        if (_hiddenTipShown)
        {
            return;
        }

        _hiddenTipShown = true;

        try
        {
            _notifyIcon.ShowBalloonTip(
                4000,
                "Dwarpal is still watching",
                "Window ta bandh hoyeche, protection chalu ache. Tray icon theke abar kholo.",
                Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Tray balloon failed");
        }
    }

    private static Drawing.Icon LoadIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var icon = Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon is not null)
                {
                    return icon;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not extract tray icon");
        }

        return Drawing.SystemIcons.Shield;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Tray icon dispose failed");
        }
    }

    private sealed class DarkColorTable : Forms.ProfessionalColorTable
    {
        public override Drawing.Color MenuItemSelected => Drawing.Color.FromArgb(41, 57, 83);
        public override Drawing.Color MenuItemBorder => Drawing.Color.FromArgb(212, 175, 55);
        public override Drawing.Color MenuBorder => Drawing.Color.FromArgb(34, 51, 79);
        public override Drawing.Color ToolStripDropDownBackground => Drawing.Color.FromArgb(12, 22, 38);
        public override Drawing.Color ImageMarginGradientBegin => Drawing.Color.FromArgb(12, 22, 38);
        public override Drawing.Color ImageMarginGradientMiddle => Drawing.Color.FromArgb(12, 22, 38);
        public override Drawing.Color ImageMarginGradientEnd => Drawing.Color.FromArgb(12, 22, 38);
        public override Drawing.Color SeparatorDark => Drawing.Color.FromArgb(34, 51, 79);
        public override Drawing.Color SeparatorLight => Drawing.Color.FromArgb(34, 51, 79);
    }
}
