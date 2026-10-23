using System.Windows;
using System.Windows.Input;
using Dwarpal.App.Services;
using Dwarpal.App.ViewModels;
using Dwarpal.Core.Models;
using Serilog;

namespace Dwarpal.App.Views;

/// <summary>
/// Sensitive action (settings, auto-start off, log view) er age credential chaay.
/// Reused PIN pad / password pattern.
/// </summary>
public partial class CredentialPromptWindow : Window
{
    private readonly CredentialPromptViewModel _viewModel;
    private readonly string _context;

    public CredentialPromptWindow(string context = "Ei kaj ta korte hole credential lagbe.")
    {
        _viewModel = new CredentialPromptViewModel { Reason = context };
        _context = context;

        InitializeComponent();

        DataContext = _viewModel;
        _viewModel.Lockout.Finished += (_, _) => FocusActiveInput();

        Loaded += async (_, _) =>
        {
            UiAnimations.FadeIn(this);
            await LoadKindAsync();
        };
    }

    private async Task LoadKindAsync()
    {
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
            Log.Warning(ex, "Could not load credential kind for prompt");
        }

        await _viewModel.Lockout.RefreshAsync();
        FocusActiveInput();
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

    private void OnPinPadSubmitted(object? sender, EventArgs e) => _ = VerifyAsync(PinPadControl.Secret);

    private void OnSecretChanged(object? sender, EventArgs e) => _viewModel.ClearError();

    private async void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await VerifyAsync(PasswordBoxInput.Password);
        }
    }

    private async void OnPasswordConfirmClick(object sender, RoutedEventArgs e)
        => await VerifyAsync(PasswordBoxInput.Password);

    private async Task VerifyAsync(string secret)
    {
        if (_viewModel.IsBusy || _viewModel.Lockout.IsLockedOut)
        {
            return;
        }

        if (string.IsNullOrEmpty(secret))
        {
            return;
        }

        _viewModel.ErrorMessage = null;
        _viewModel.IsBusy = true;

        try
        {
            var result = await AppServices.Credentials.VerifyAsync(secret, _context);

            if (result.Success)
            {
                DialogResult = true;
                return;
            }

            _viewModel.ErrorMessage = result.Error ?? "Credential mileni.";

            if (result.LockoutSeconds > 0)
            {
                _viewModel.Lockout.Start(result.LockoutSeconds);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Credential prompt verification failed");
            _viewModel.ErrorMessage = "Verify korte parlam na: " + ex.Message;
        }
        finally
        {
            _viewModel.IsBusy = false;
        }

        PinPadControl.Clear();
        PasswordBoxInput.Clear();
        FocusActiveInput();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnDragAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
