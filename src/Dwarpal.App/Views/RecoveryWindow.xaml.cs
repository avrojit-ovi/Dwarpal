using System.Windows;
using System.Windows.Input;
using Dwarpal.App.Services;
using Dwarpal.App.ViewModels;
using Dwarpal.Core.Models;
using Serilog;

namespace Dwarpal.App.Views;

public partial class RecoveryWindow : Window
{
    private readonly RecoveryViewModel _viewModel;

    public RecoveryWindow()
    {
        // RadioButton.Checked InitializeComponent er somoy fire kore.
        _viewModel = new RecoveryViewModel();

        InitializeComponent();

        DataContext = _viewModel;

        Loaded += (_, _) =>
        {
            UiAnimations.FadeIn(this);
            RecoveryKeyBox.Focus();
        };
    }

    private void OnPinChecked(object sender, RoutedEventArgs e) => _viewModel.Kind = CredentialKind.Pin;

    private void OnPasswordChecked(object sender, RoutedEventArgs e) => _viewModel.Kind = CredentialKind.Password;

    private async void OnResetClick(object sender, RoutedEventArgs e)
    {
        var success = await _viewModel.TryResetAsync(NewSecretBox.Password, ConfirmSecretBox.Password);

        if (success)
        {
            NewSecretBox.Clear();
            ConfirmSecretBox.Clear();
        }
    }

    private void OnCopyNewKeyClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_viewModel.NewRecoveryKey))
        {
            return;
        }

        try
        {
            Clipboard.SetText(_viewModel.NewRecoveryKey);
            ResultStatusText.Text = "Recovery key clipboard e copy hoyeche.";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Clipboard copy failed");
            ResultStatusText.Text = "Copy korte parlam na - key ta hate likhe nite paren.";
        }
    }

    private void OnPrintNewKeyClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_viewModel.NewRecoveryKey))
        {
            RecoveryKeyPrinter.Print(_viewModel.NewRecoveryKey, this);
        }
    }

    private void OnDoneClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnCloseClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnDragAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
