using System.Windows;
using System.Windows.Threading;
using Dwarpal.App.Services;
using Dwarpal.App.Views;
using Dwarpal.Core.Data;
using Dwarpal.Core.Models;
using Dwarpal.Core.Security;
using Dwarpal.Core.Services;
using Serilog;

namespace Dwarpal.App;

public partial class App : Application
{
    private SingleInstance? _singleInstance;
    private TrayIconService? _tray;
    private PanicHotkeyService? _panicHotkey;
    private bool _exiting;
    private bool _isPrimaryInstance;

    public bool HardeningApplied { get; private set; }
    public string? HardeningError { get; private set; }
    public bool IsShuttingDown => _exiting || Dispatcher.HasShutdownStarted;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Main window swap/hide korar somoy app accidentally exit na kore.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        // Watchdog nijei ekta Dwarpal process - full app start korar dorkar nei.
        if (e.Args.Any(argument => string.Equals(argument, "--watchdog", StringComparison.OrdinalIgnoreCase)))
        {
            Shutdown(WatchdogService.RunWatchdog());
            return;
        }

        _singleInstance = SingleInstance.TryAcquire(
            () => Dispatcher.BeginInvoke(new Action(ShowMainWindow)),
            out var alreadyRunning);

        if (alreadyRunning)
        {
            SingleInstance.SignalExisting();
            Shutdown(0);
            return;
        }

        // Secondary/watchdog exits must not touch the primary instance's watchdog task.
        _isPrimaryInstance = true;

        SessionEnding += (_, _) =>
        {
            _exiting = true;

            if (MainWindow is Views.MainWindow main)
            {
                main.AllowClose();
            }
            else if (MainWindow is Views.UnlockWindow unlock)
            {
                unlock.AllowClose();
            }
        };

        try
        {
            await AppServices.InitializeAsync();

            _panicHotkey = AppServices.PanicHotkey;
            _panicHotkey.Pressed += OnPanicHotkeyPressed;
            _panicHotkey.Register();

            await ApplyProcessHardeningAsync();

            if (await AppServices.AppSettings.GetWatchdogEnabledAsync())
            {
                WatchdogService.EnsureEnabled();
            }

            var isConfigured = await AppServices.Credentials.IsConfiguredAsync();

            // Protection engine startup gate er agei chalu hobe. Nahole unlock window
            // minimize kore rakhe protected app khulle segulo lock na hoye jete pare.
            if (isConfigured && AppServices.Protection is { IsRunning: false } protection)
            {
                await protection.StartAsync();
            }

            Window startupWindow = isConfigured ? new UnlockWindow() : new SetupWindow();

            MainWindow = startupWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            startupWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Dwarpal startup failed");

            // Failing app ke watchdog bar bar restart kore loop banabe na.
            if (await IsWatchdogEnabledAsync())
            {
                WatchdogService.Disable();
            }

            MessageBox.Show(
                "Dwarpal start korte parlam na.\n\n" + ex.Message,
                "Dwarpal", MessageBoxButton.OK, MessageBoxImage.Error);

            _exiting = true;
            Shutdown(1);
        }
    }

    private async Task ApplyProcessHardeningAsync()
    {
        if (ProcessHardening.IsDisabledByEnvironment)
        {
            HardeningApplied = false;
            HardeningError = $"{ProcessHardening.DisableEnvironmentVariable}=1";
            AppServices.ProcessHardeningActive = false;
            AppServices.ProcessHardeningError = HardeningError;
            Log.Warning("Process DACL hardening disabled by environment variable");
            await LogHardeningAsync(ActivityOutcome.Info, "DACL hardening skipped by environment variable.");
            return;
        }

        if (ProcessHardening.TryApplyToCurrentProcess(out var error))
        {
            HardeningApplied = true;
            HardeningError = null;
            Log.Information("Process DACL hardening active (deny PROCESS_TERMINATE to Users)");
        }
        else
        {
            HardeningApplied = false;
            HardeningError = error;
            Log.Warning("Process DACL hardening failed: {Error}", error);
        }

        AppServices.ProcessHardeningActive = HardeningApplied;
        AppServices.ProcessHardeningError = HardeningError;

        await LogHardeningAsync(
            HardeningApplied ? ActivityOutcome.Success : ActivityOutcome.Failure,
            HardeningApplied
                ? "Process DACL applied: PROCESS_TERMINATE denied to Users."
                : "Process DACL failed: " + HardeningError);
    }

    /// <summary>
    /// Panic hotkey: schedule window ignore kore ekhoni sob running protected app lock kore.
    /// Credential chara ar kono protected app front e ana jabe na.
    /// </summary>
    private async void OnPanicHotkeyPressed(object? sender, EventArgs e)
    {
        try
        {
            var protection = AppServices.Protection;

            if (protection is null)
            {
                return;
            }

            var locked = await protection.LockAllRunningAsync();

            await AppServices.ActivityLog.LogAsync(
                ActivityEventType.Lock,
                ActivityOutcome.Info,
                $"Panic hotkey ({PanicHotkeyService.DisplayChord}) locked {locked} running protected app(s).");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Panic hotkey action failed");
        }
    }

    private static async Task LogHardeningAsync(ActivityOutcome outcome, string detail)
    {
        try
        {
            await AppServices.ActivityLog.LogAsync(ActivityEventType.Tamper, outcome, detail);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not write hardening activity entry");
        }
    }

    /// <summary>
    /// Setup/unlock sesh hole main window show kore, purano window gulo bondho kore dey.
    /// </summary>
    public void OpenMainWindow()
    {
        var main = new Views.MainWindow();
        var previous = MainWindow;

        MainWindow = main;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        EnsureTray();
        main.Show();

        // Shudhu startup gate ta bondho koro - kono active app lock screen close koro na.
        if (previous is not null && !ReferenceEquals(previous, main))
        {
            if (previous is Views.UnlockWindow unlock)
            {
                unlock.AllowClose();
            }

            previous.Close();
        }
    }

    /// <summary>Tray ba second launch theke window abar show kore.</summary>
    public void ShowMainWindow()
    {
        if (MainWindow is Views.MainWindow main)
        {
            main.Show();
            main.WindowState = WindowState.Normal;
            main.Activate();
            main.Topmost = true;
            main.Topmost = false;
            return;
        }

        MainWindow?.Activate();
    }

    public void NotifyMinimizedToTray() => _tray?.ShowHiddenNotification();

    private void EnsureTray()
    {
        _tray ??= new TrayIconService(ShowMainWindow, () => _ = RequestExitAsync());
    }

    /// <summary>Tray Exit: credential chara app bondho kora jabe na.</summary>
    private async Task RequestExitAsync()
    {
        if (_exiting)
        {
            return;
        }

        try
        {
            var configured = await AppServices.Credentials.IsConfiguredAsync();

            if (configured)
            {
                var prompt = new CredentialPromptWindow("Dwarpal theke exit korte hole credential lagbe.");
                if (prompt.ShowDialog() != true)
                {
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exit confirmation failed");
            MessageBox.Show(
                "Credential verify korte parlam na. Exit cancel kora holo.",
                "Dwarpal", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _exiting = true;

        try
        {
            if (await AppServices.AppSettings.GetWatchdogEnabledAsync())
            {
                WatchdogService.Disable();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not remove watchdog task on exit");
        }

        if (MainWindow is Views.MainWindow main)
        {
            main.AllowClose();
        }

        _tray?.Dispose();
        _tray = null;

        Shutdown(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Graceful exit hole watchdog task remove koro, nahole app abar restart hobe.
        // Crash/kill (OnExit chole na) hole task theke jay - tamper protection active thake.
        // Secondary instance ba --watchdog process primary task ke touch korbe na.
        if (_isPrimaryInstance && e.ApplicationExitCode == 0)
        {
            try
            {
                WatchdogService.Disable();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not remove watchdog task on graceful exit");
            }
        }

        // App exit hole suspend kora process gulo resume kore dey.
        AppServices.Protection?.Dispose();

        if (_panicHotkey is not null)
        {
            _panicHotkey.Pressed -= OnPanicHotkeyPressed;
            _panicHotkey.Dispose();
            _panicHotkey = null;
        }

        _tray?.Dispose();
        _tray = null;
        _singleInstance?.Dispose();
        _singleInstance = null;

        Log.Information("Dwarpal exiting with code {ExitCode}", e.ApplicationExitCode);
        Log.CloseAndFlush();

        base.OnExit(e);
    }

    private static async Task<bool> IsWatchdogEnabledAsync()
    {
        try
        {
            return await AppServices.AppSettings.GetWatchdogEnabledAsync();
        }
        catch
        {
            return false;
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception");

        if (Current is App app)
        {
            app._exiting = true;
        }

        MessageBox.Show(
            "Ekta unexpected error hoyeche. Details log file e ache.\n\n" + e.Exception.Message,
            "Dwarpal", MessageBoxButton.OK, MessageBoxImage.Error);

        // App ke zombie state e rakhar cheye bondho kora bhalo.
        e.Handled = true;
        Current.Shutdown(1);
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Log.Fatal(exception, "Unhandled domain exception");
            Log.CloseAndFlush();
        }
    }
}
