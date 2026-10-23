using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Dwarpal.App.Services;
using Dwarpal.App.ViewModels;
using Dwarpal.Core.Models;
using Dwarpal.Core.Processes;
using Serilog;

namespace Dwarpal.App.Views;

public partial class LockScreenWindow : Window
{
    private readonly LockScreenContext _context;
    private readonly LockScreenViewModel _viewModel = new();
    private readonly DispatcherTimer _processTimer;
    private bool _allowClose;

    public LockScreenWindow(LockScreenContext context)
    {
        _context = context;

        InitializeComponent();

        DataContext = _viewModel;
        _viewModel.Lockout.Finished += (_, _) => FocusActiveInput();
        _viewModel.AppName = context.App.DisplayName;
        _viewModel.AppPath = context.App.ExecutablePath;
        _viewModel.AppIcon = context.Icon;

        BackgroundImage.Source = ScreenSnapshot.CaptureVirtualScreen();

        Loaded += OnLoaded;
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;
        KeyDown += OnWindowKeyDown;
        Closed += (_, _) => _processTimer?.Stop();

        _processTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _processTimer.Tick += OnProcessTimerTick;
    }

    public string AppName => _viewModel.AppName;
    public string AppPath => _viewModel.AppPath;
    public ProtectedApp App => _context.App;

    public event EventHandler? Unlocked;
    public event EventHandler? ProcessExited;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        UiAnimations.FadeIn(this, 180);
        Activate();

        try
        {
            var kind = await AppServices.Credentials.GetKindAsync();
            if (kind is not null)
            {
                _viewModel.Kind = kind.Value;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not load credential kind for lock screen");
        }

        await _viewModel.Lockout.RefreshAsync();
        FocusActiveInput();
        _processTimer.Start();
    }

    private void FocusActiveInput()
    {
        if (!_viewModel.CanAttempt)
        {
            return;
        }

        if (_viewModel.IsPin)
        {
            PinPadControl.FocusInput();
        }
        else
        {
            PasswordBoxInput.Focus();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Alt+F4 / Escape diye lock screen bondho kora jabe na.
        if ((e.Key == Key.System && e.SystemKey == Key.F4)
            || (e.Key == Key.F4 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            || e.Key == Key.Escape)
        {
            e.Handled = true;
        }
    }

    private async void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        // Focus kono button/tab e chole gele o physical keyboard er Enter kaj korbe.
        if (e.Key == Key.Enter && _viewModel.IsPin)
        {
            e.Handled = true;
            await TryUnlockAsync(PinPadControl.Secret);
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // App shutdown er somoy close block korle process atke jete pare.
        var appIsShuttingDown = Application.Current?.Dispatcher.HasShutdownStarted == true;

        if (!_allowClose && !appIsShuttingDown)
        {
            e.Cancel = true;
        }
    }

    private void OnProcessTimerTick(object? sender, EventArgs e)
    {
        if (_context.IsAnyProcessRunning())
        {
            return;
        }

        _processTimer.Stop();
        ProcessExited?.Invoke(this, EventArgs.Empty);
    }

    private void OnPinPadSubmitted(object? sender, EventArgs e) => _ = TryUnlockAsync(PinPadControl.Secret);

    private void OnSecretChanged(object? sender, EventArgs e) => _viewModel.ClearError();

    private async void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await TryUnlockAsync(PasswordBoxInput.Password);
        }
    }

    private async void OnUnlockPasswordClick(object sender, RoutedEventArgs e)
        => await TryUnlockAsync(PasswordBoxInput.Password);

    private async Task TryUnlockAsync(string secret)
    {
        if (!_viewModel.CanAttempt)
        {
            return;
        }

        if (string.IsNullOrEmpty(secret))
        {
            _viewModel.ErrorMessage = _viewModel.IsPin ? "PIN ta dao." : "Password ta dao.";
            return;
        }

        _viewModel.ErrorMessage = null;
        _viewModel.IsBusy = true;

        try
        {
            var result = await _context.VerifyAsync(secret);

            if (result.Success)
            {
                Unlocked?.Invoke(this, EventArgs.Empty);
                return;
            }

            _viewModel.ErrorMessage = result.Error ?? "Unlock fail hoyeche.";

            if (result.LockoutSeconds > 0)
            {
                _viewModel.Lockout.Start(result.LockoutSeconds);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Lock screen unlock failed");
            _viewModel.ErrorMessage = "Unlock korte parlam na: " + ex.Message;
        }
        finally
        {
            _viewModel.IsBusy = false;
        }

        PinPadControl.Clear();
        PasswordBoxInput.Clear();
        FocusActiveInput();
    }

    private async void OnForgotClick(object sender, RoutedEventArgs e)
    {
        var recoveryWindow = new RecoveryWindow { Owner = this };

        try
        {
            recoveryWindow.ShowDialog();
        }
        finally
        {
            try
            {
                var kind = await AppServices.Credentials.GetKindAsync();
                if (kind is not null)
                {
                    _viewModel.Kind = kind.Value;
                }

                // Recovery successful hole DB lockout clear hoy; local countdown o
                // clear korte hobe, nahole notun credential diyeo input disabled thake.
                await _viewModel.Lockout.RefreshAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not refresh credential state after recovery");
            }

            PinPadControl.Clear();
            PasswordBoxInput.Clear();
            FocusActiveInput();
        }
    }

    /// <summary>Engine theke call hoy - normally closing block kora thake.</summary>
    public void CloseForUnlock()
    {
        _allowClose = true;
        _processTimer.Stop();
        Close();
    }
}
