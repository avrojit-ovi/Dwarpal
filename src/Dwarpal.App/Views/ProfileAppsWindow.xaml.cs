using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Dwarpal.App.Services;
using Serilog;

namespace Dwarpal.App.Views;

/// <summary>Dialog er checkbox row. IsSelected binding er jonno mutable.</summary>
public sealed class ProfileAppOption
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;
    public bool IsSelected { get; set; }
}

public partial class ProfileAppsWindow : Window
{
    private readonly int _profileId;

    public ProfileAppsWindow(int profileId, string profileName)
    {
        InitializeComponent();

        _profileId = profileId;
        ProfileNameText.Text = profileName;
        AppsList.ItemsSource = Options;

        Loaded += async (_, _) =>
        {
            UiAnimations.FadeIn(this);
            await LoadAsync();
        };
    }

    public ObservableCollection<ProfileAppOption> Options { get; } = new();

    public IReadOnlyCollection<int> SelectedAppIds { get; private set; } = Array.Empty<int>();

    private async Task LoadAsync()
    {
        try
        {
            var apps = await AppServices.ProtectedApps.GetAllAsync();
            var assigned = (await AppServices.Profiles.GetAppIdsAsync(_profileId)).ToHashSet();

            Options.Clear();

            foreach (var app in apps)
            {
                Options.Add(new ProfileAppOption
                {
                    Id = app.Id,
                    DisplayName = app.DisplayName,
                    ExecutablePath = app.ExecutablePath,
                    IsSelected = assigned.Contains(app.Id),
                });
            }

            EmptyText.Visibility = Options.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateSelectionText();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Loading profile apps failed");
            EmptyText.Text = "App list load korte parlam na: " + ex.Message;
            EmptyText.Visibility = Visibility.Visible;
        }
    }

    private void OnSelectionChanged(object sender, RoutedEventArgs e) => UpdateSelectionText();

    private void UpdateSelectionText()
    {
        var count = Options.Count(option => option.IsSelected);
        SelectionText.Text = count == 0
            ? "Kono app select kora nei — ei profile e kichu lock hobe na."
            : $"{count} app selected";
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        SelectedAppIds = Options.Where(option => option.IsSelected).Select(option => option.Id).ToList();
        DialogResult = true;
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
