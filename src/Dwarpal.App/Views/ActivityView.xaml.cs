using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dwarpal.App.ViewModels;
using Microsoft.Win32;

namespace Dwarpal.App.Views;

public partial class ActivityView : UserControl
{
    public ActivityView()
    {
        InitializeComponent();
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e) => await SearchAsync();

    private async void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SearchAsync();
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActivityViewModel viewModel)
        {
            await viewModel.LoadAsync();
        }
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ActivityViewModel viewModel)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export Dwarpal activity",
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"dwarpal-activity-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            AddExtension = true,
            DefaultExt = ".csv",
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            await viewModel.ExportCsvAsync(dialog.FileName);
        }
    }

    private void OnSelfieClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ActivityLogRow row }
            || string.IsNullOrWhiteSpace(row.SelfiePath))
        {
            return;
        }

        var window = new SelfieWindow(row.SelfiePath, row.Title, row.DateText + " " + row.TimeText)
        {
            Owner = Window.GetWindow(this),
        };

        window.ShowDialog();
    }

    private async Task SearchAsync()
    {
        if (DataContext is ActivityViewModel viewModel)
        {
            await viewModel.LoadAsync();
        }
    }
}
