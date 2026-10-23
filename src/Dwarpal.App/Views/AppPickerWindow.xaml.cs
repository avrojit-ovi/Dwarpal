using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Dwarpal.App.Services;
using Dwarpal.App.ViewModels;
using Dwarpal.Core.Applications;
using Microsoft.Win32;

namespace Dwarpal.App.Views;

public partial class AppPickerWindow : Window
{
    private readonly AppPickerViewModel _viewModel = new();

    public AppPickerWindow()
    {
        InitializeComponent();

        DataContext = _viewModel;

        Loaded += async (_, _) =>
        {
            UiAnimations.FadeIn(this);
            await _viewModel.LoadAsync();
        };
    }

    /// <summary>Dialog true hole ei app ta protected list e add hobe.</summary>
    public InstalledApp? AddedApp { get; private set; }

    private void OnAddClick(object sender, RoutedEventArgs e) => CommitSelection();

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e) => CommitSelection();

    private void CommitSelection()
    {
        if (_viewModel.SelectedApp is not { } selected)
        {
            return;
        }

        AddedApp = new InstalledApp(
            selected.Name,
            selected.Path,
            selected.IconPath,
            selected.Source);

        DialogResult = true;
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select an application",
            Filter = "Applications (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var path = dialog.FileName;

        AddedApp = new InstalledApp(
            GetDisplayName(path),
            path,
            path,
            InstalledAppSource.Manual);

        DialogResult = true;
    }

    private static string GetDisplayName(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);

            if (!string.IsNullOrWhiteSpace(info.ProductName))
            {
                return info.ProductName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(info.FileDescription))
            {
                return info.FileDescription.Trim();
            }
        }
        catch (Exception)
        {
            // Version info na thakle filename diye cholbe.
        }

        return Path.GetFileNameWithoutExtension(path);
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
