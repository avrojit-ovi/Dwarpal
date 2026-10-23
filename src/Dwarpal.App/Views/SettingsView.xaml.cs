using System.Windows;
using System.Windows.Controls;
using Dwarpal.App.ViewModels;

namespace Dwarpal.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void OnAutoStartClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel
            || sender is not CheckBox checkBox)
        {
            return;
        }

        await viewModel.SetAutoStartAsync(checkBox.IsChecked == true);
    }

    private async void OnSelfieClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel
            || sender is not CheckBox checkBox)
        {
            return;
        }

        await viewModel.SetSelfieAsync(checkBox.IsChecked == true);
    }

    private async void OnWatchdogClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel
            || sender is not CheckBox checkBox)
        {
            return;
        }

        await viewModel.SetWatchdogAsync(checkBox.IsChecked == true);
    }
}
