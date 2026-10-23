using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Dwarpal.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public DashboardViewModel Dashboard { get; } = new();
    public ProtectedAppsViewModel ProtectedApps { get; } = new();
    public ProfilesViewModel Profiles { get; } = new();
    public ScheduleViewModel Schedule { get; } = new();
    public ActivityViewModel Activity { get; } = new();
    public SettingsViewModel Settings { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboard), nameof(IsProtectedApps), nameof(IsProfiles), nameof(IsSchedule), nameof(IsActivity), nameof(IsSettings))]
    private string _currentPage = "dashboard";

    [ObservableProperty]
    private string _pageTitle = "Dashboard";

    [ObservableProperty]
    private string _pageSubtitle = "Your guard at a glance";

    public MainViewModel()
    {
        ProtectedApps.AppsChanged += async (_, _) => await Dashboard.LoadAsync();
        Profiles.ProfilesChanged += async (_, _) =>
        {
            await Dashboard.LoadAsync();
            await Profiles.LoadAsync();
        };
    }

    public bool IsDashboard => CurrentPage == "dashboard";
    public bool IsProtectedApps => CurrentPage == "protected-apps";
    public bool IsProfiles => CurrentPage == "profiles";
    public bool IsSchedule => CurrentPage == "schedule";
    public bool IsActivity => CurrentPage == "activity";
    public bool IsSettings => CurrentPage == "settings";

    public string VersionText =>
        $"Dwarpal {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    public async Task LoadAsync()
    {
        await Dashboard.LoadAsync();
        await ProtectedApps.LoadAsync();
        await Settings.LoadAsync();
    }

    [RelayCommand]
    private async Task NavigateAsync(string? page)
    {
        if (string.IsNullOrWhiteSpace(page))
        {
            return;
        }

        CurrentPage = page;

        switch (page)
        {
            case "protected-apps":
                PageTitle = "Protected Apps";
                PageSubtitle = "Apps that ask for your code before opening";
                await ProtectedApps.LoadAsync();
                break;

            case "profiles":
                PageTitle = "Profiles";
                PageSubtitle = "Named app sets — ekta profile ek somoy e active";
                await Profiles.LoadAsync();
                break;

            case "schedule":
                PageTitle = "Schedule";
                PageSubtitle = "Protection window — kokhon app lock hobe";
                await Schedule.LoadAsync();
                break;

            case "activity":
                PageTitle = "Activity Log";
                PageSubtitle = "Unlock, tamper ar settings er audit trail";
                await Activity.LoadAsync();
                break;

            case "settings":
                PageTitle = "Settings";
                PageSubtitle = "Re-lock, startup ar security preferences";
                await Settings.LoadAsync();
                break;

            default:
                PageTitle = "Dashboard";
                PageSubtitle = "Your guard at a glance";
                await Dashboard.LoadAsync();
                break;
        }
    }
}
