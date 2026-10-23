using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Dwarpal.App.Services;
using Dwarpal.Core.Applications;
using Serilog;

namespace Dwarpal.App.ViewModels;

public sealed record InstalledAppRow(string Name, string Path, string? IconPath, ImageSource? Icon, InstalledAppSource Source);

public partial class AppPickerViewModel : ObservableObject
{
    private readonly List<InstalledAppRow> _all = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    private InstalledAppRow? _selectedApp;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public ObservableCollection<InstalledAppRow> Apps { get; } = new();

    public bool CanAdd => SelectedApp is not null;

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var apps = await AppServices.InstalledApps.ScanAsync();

            _all.Clear();
            foreach (var app in apps)
            {
                _all.Add(new InstalledAppRow(
                    app.DisplayName,
                    app.ExecutablePath,
                    app.IconPath,
                    IconLoader.Load(app.IconPath ?? app.ExecutablePath, 24),
                    app.Source));
            }

            ApplyFilter();
            StatusText = $"{_all.Count} installed app found";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Installed app scan failed");
            ErrorMessage = "Installed app list load korte parlam na: " + ex.Message;
            StatusText = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = SearchText.Trim();

        Apps.Clear();
        foreach (var app in _all)
        {
            if (query.Length == 0
                || app.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || app.Path.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            {
                Apps.Add(app);
            }
        }

        if (!IsLoading)
        {
            StatusText = $"{Apps.Count} app";
        }
    }
}
