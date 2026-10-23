using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Dwarpal.App.Services;
using Dwarpal.App.ViewModels;
using Dwarpal.Core.Models;
using Serilog;

namespace Dwarpal.App.Views;

public partial class SetupWindow : Window
{
    private readonly SetupViewModel _viewModel;

    public SetupWindow()
    {
        // XAML e IsChecked="True" thakle Checked event InitializeComponent er somoy
        // fire kore - tai VM agei create korte hoy.
        _viewModel = new SetupViewModel();

        InitializeComponent();

        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += (_, _) =>
        {
            UiAnimations.FadeIn(this);
            UpdateSecretInputs();
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SetupViewModel.Step))
        {
            UpdateSecretInputs();
        }
    }

    private void UpdateSecretInputs()
    {
        // PIN mode e max 8, password mode e 64.
        var maxLength = _viewModel.Kind == CredentialKind.Pin ? 8 : 64;
        SecretBox.MaxLength = maxLength;
        ConfirmBox.MaxLength = maxLength;

        if (_viewModel.Step == SetupStep.Credential)
        {
            Dispatcher.BeginInvoke(new Action(() => SecretBox.Focus()));
        }
    }

    private void OnPinChecked(object sender, RoutedEventArgs e) => _viewModel.Kind = CredentialKind.Pin;

    private void OnPasswordChecked(object sender, RoutedEventArgs e) => _viewModel.Kind = CredentialKind.Password;

    private async void OnCredentialContinueClick(object sender, RoutedEventArgs e) => await SubmitCredentialAsync();

    private async void OnSecretKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SubmitCredentialAsync();
        }
    }

    private async Task SubmitCredentialAsync()
    {
        var success = await _viewModel.SubmitCredentialAsync(SecretBox.Password, ConfirmBox.Password);

        if (success)
        {
            // Plaintext reference UI theke soriye felchi.
            SecretBox.Clear();
            ConfirmBox.Clear();
            CopyStatusText.Text = string.Empty;
        }
        else
        {
            SecretBox.Focus();
            SecretBox.SelectAll();
        }
    }

    private void OnCopyRecoveryKeyClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_viewModel.RecoveryKey))
        {
            return;
        }

        try
        {
            Clipboard.SetText(_viewModel.RecoveryKey);
            CopyStatusText.Text = "Recovery key clipboard e copy hoyeche.";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Clipboard copy failed");
            CopyStatusText.Text = "Copy korte parlam na - key ta hate likhe nite paren.";
        }
    }

    private void OnPrintRecoveryKeyClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_viewModel.RecoveryKey))
        {
            return;
        }

        RecoveryKeyPrinter.Print(_viewModel.RecoveryKey, this);
    }

    private async void OnRecoveryContinueClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.ContinueFromRecoveryAsync();
    }

    private void OnFinishClick(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).OpenMainWindow();
    }

    private void OnDragAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
