using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dwarpal.App.ViewModels;

namespace Dwarpal.App.Views;

public partial class ProfilesView : UserControl
{
    public ProfilesView()
    {
        InitializeComponent();
    }

    private ProfilesViewModel? ViewModel => DataContext as ProfilesViewModel;

    private async void OnCreateClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.CreateAsync();
        }
    }

    private async void OnNameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel is not null)
        {
            e.Handled = true;
            await ViewModel.CreateAsync();
        }
    }

    private async void OnAllAppsClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ActivateAllAppsAsync();
        }
    }

    private async void OnActivateClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null && RowOf(sender) is { } row)
        {
            await ViewModel.ActivateAsync(row);
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || RowOf(sender) is not { } row)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        var confirm = MessageBox.Show(
            owner,
            $"'{row.Name}' profile delete korbe? App assignment gulo o chole jabe, apps nijer moto protected thake.",
            "Delete profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            await ViewModel.DeleteAsync(row);
        }
    }

    private async void OnAppsClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || RowOf(sender) is not { } row)
        {
            return;
        }

        var dialog = new ProfileAppsWindow(row.Id, row.Name)
        {
            Owner = Window.GetWindow(this),
        };

        if (dialog.ShowDialog() == true)
        {
            await ViewModel.SaveAppsAsync(row.Id, dialog.SelectedAppIds);
        }
    }

    private static ProfileRow? RowOf(object sender)
        => (sender as FrameworkElement)?.DataContext as ProfileRow;
}
