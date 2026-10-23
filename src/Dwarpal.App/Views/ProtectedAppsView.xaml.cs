using System.Windows;
using System.Windows.Controls;
using Dwarpal.App.ViewModels;

namespace Dwarpal.App.Views;

public partial class ProtectedAppsView : UserControl
{
    public ProtectedAppsView()
    {
        InitializeComponent();
    }

    private async void OnAddAppClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProtectedAppsViewModel viewModel)
        {
            return;
        }

        var picker = new AppPickerWindow { Owner = Window.GetWindow(this) };

        if (picker.ShowDialog() == true && picker.AddedApp is not null)
        {
            await viewModel.AddAppAsync(picker.AddedApp.DisplayName, picker.AddedApp.ExecutablePath);
        }
    }

    private async void OnToggleClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProtectedAppsViewModel viewModel
            || sender is not CheckBox { DataContext: ProtectedAppRow row } checkBox)
        {
            return;
        }

        await viewModel.SetEnabledAsync(row, checkBox.IsChecked == true);
    }

    private async void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProtectedAppsViewModel viewModel
            || sender is not FrameworkElement { DataContext: ProtectedAppRow row })
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"\"{row.DisplayName}\" ke protected list theke remove korbe?",
            "Dwarpal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            await viewModel.RemoveAsync(row);
        }
    }
}
