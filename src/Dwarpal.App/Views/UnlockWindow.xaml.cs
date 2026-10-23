using System.Windows;
using System.Windows.Input;
using Dwarpal.App.Services;
using Dwarpal.App.ViewModels;

namespace Dwarpal.App.Views;

public partial class UnlockWindow : Window
{
    private readonly UnlockViewModel _viewModel;

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

        try
        {
            recoveryWindow.ShowDialog();
        }
        finally
        {
            // Recovery fail koruk ba cancel - kind change hoye thakle o reload korte hobe.
            await _viewModel.LoadAsync();
            PinPadControl.Clear();
            PasswordBoxInput.Clear();
            InfoText.Text = "Credential reset hoye gele notun code diye unlock koro.";
            FocusActiveInput();
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void OnDragAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
