using System.Windows;
using System.Windows.Controls;
using Dwarpal.App.ViewModels;

namespace Dwarpal.App.Views;

public partial class ScheduleView : UserControl
{
    public ScheduleView()
    {
        InitializeComponent();
    }

    private ScheduleViewModel? ViewModel => DataContext as ScheduleViewModel;

    private async void OnAddClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.AddAsync();
        }
    }

    private void OnStartBackClick(object sender, RoutedEventArgs e) => ViewModel?.ShiftStart(-1);

    private void OnStartForwardClick(object sender, RoutedEventArgs e) => ViewModel?.ShiftStart(+1);

    private void OnEndBackClick(object sender, RoutedEventArgs e) => ViewModel?.ShiftEnd(-1);

    private void OnEndForwardClick(object sender, RoutedEventArgs e) => ViewModel?.ShiftEnd(+1);

    private async void OnToggleClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null
            || sender is not CheckBox checkBox
            || (sender as FrameworkElement)?.DataContext is not ScheduleRow row)
        {
            return;
        }

        await ViewModel.SetEnabledAsync(row, checkBox.IsChecked == true);
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || (sender as FrameworkElement)?.DataContext is not ScheduleRow row)
        {
            return;
        }

        var confirm = MessageBox.Show(
            Window.GetWindow(this),
            $"'{row.Name}' ({row.Summary}) delete korbe?",
            "Delete window",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            await ViewModel.DeleteAsync(row);
        }
    }
}
