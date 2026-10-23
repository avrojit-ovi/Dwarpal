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

        // OneWay binding + a failed Task Scheduler call can leave the visual state
        // out of sync because the VM reverted to the same value it already had.
        checkBox.IsChecked = viewModel.AutoStartEnabled;
    }

    private async void OnSelfieClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel
            || sender is not CheckBox checkBox)
        {
            return;
        }

        await viewModel.SetSelfieAsync(checkBox.IsChecked == true);
        checkBox.IsChecked = viewModel.SelfieEnabled;
    }

    private async void OnWatchdogClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel
            || sender is not CheckBox checkBox)
        {
            return;
        }

        await viewModel.SetWatchdogAsync(checkBox.IsChecked == true);
        checkBox.IsChecked = viewModel.WatchdogEnabled;
    }
}
