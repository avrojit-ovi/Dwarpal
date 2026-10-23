using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Dwarpal.App.Services;
using Dwarpal.App.ViewModels;

namespace Dwarpal.App.Views;

public partial class UnlockWindow : Window
{
    private readonly UnlockViewModel _viewModel;
    private bool _allowClose;

    public UnlockWindow()
    {
        InitializeComponent();

        _viewModel = new UnlockViewModel();
        DataContext = _viewModel;
        _viewModel.Unlocked += (_, _) => ((App)Application.Current).OpenMainWindow();
        _viewModel.Lockout.Finished += (_, _) => FocusActiveInput();

        Loaded += async (_, _) =>
        {
            UiAnimations.FadeIn(this);
            await _viewModel.LoadAsync();
            FocusActiveInput();
        };

        Closing += OnClosing;
    }

    /// <summary>App startup / Windows shutdown er somoy gate ta close korte dey.</summary>
    public void AllowClose() => _allowClose = true;

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var appIsShuttingDown = Application.Current is App app && app.IsShuttingDown;

        // Startup credential gate ta close/minimize kore protection bypass kora jabe na.
        if (!_allowClose && !appIsShuttingDown)
        {
            e.Cancel = true;
        }
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

    private void OnPinPadSubmitted(object? sender, EventArgs e) => _ = TryUnlockAsync(PinPadControl.Secret);

    private void OnSecretChanged(object? sender, EventArgs e) => _viewModel.ClearError();

    private async void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        // PIN pad er input e focus na thakleo Enter diye unlock kora jabe.
        if (e.Key == Key.Enter && _viewModel.IsPin)
        {
            e.Handled = true;
            await TryUnlockAsync(PinPadControl.Secret);
        }
    }

    private async void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await TryUnlockAsync(PasswordBoxInput.Password);
        }
    }

    private async void OnUnlockPasswordClick(object sender, RoutedEventArgs e) => await TryUnlockAsync(PasswordBoxInput.Password);

    private async Task TryUnlockAsync(string secret)
    {
        var success = await _viewModel.TryUnlockAsync(secret);

        if (success)
        {
            return;
        }

        // Fail hole input clear kore dey, jate user abar clean vabe type korte pare.
        PinPadControl.Clear();
        PasswordBoxInput.Clear();
        FocusActiveInput();
    }

    private async void OnForgotClick(object sender, RoutedEventArgs e)
    {
        var recoveryWindow = new RecoveryWindow { Owner = this };
        var reset = false;

        try
        {
            reset = recoveryWindow.ShowDialog() == true;
        }
        finally
        {
            // Recovery fail koruk ba cancel - kind change hoye thakle o reload korte hobe.
            await _viewModel.LoadAsync();
            PinPadControl.Clear();
            PasswordBoxInput.Clear();
            InfoText.Text = reset
                ? "Credential reset hoye gele notun code diye unlock koro."
                : string.Empty;
            FocusActiveInput();
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnDragAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
