using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Dwarpal.App.Services;
using Dwarpal.Core.Models;
using Serilog;

namespace Dwarpal.App.ViewModels;

public sealed class ProfileRow
{
    public ProfileRow(Profile profile, bool isActive)
    {
        Id = profile.Id;
        Name = profile.Name;
        AppCount = profile.AppCount;
        IsActive = isActive;
    }

    public int Id { get; }
    public string Name { get; }
    public int AppCount { get; }
    public bool IsActive { get; }

    public string AppCountText => AppCount == 1 ? "1 protected app" : $"{AppCount} protected apps";
    public bool CanActivate => !IsActive;
    public bool CanDelete => !IsActive;
}

public partial class ProfilesViewModel : ObservableObject
{
    [ObservableProperty]
    private string _newProfileName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfiles), nameof(HasNoProfiles))]
    private bool _hasAnyProfiles;

    [ObservableProperty]
    private string _activeProfileText = "All protected apps";

    [ObservableProperty]
    private string _activeProfileSummary = "One active profile decides which apps ask for your code.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveProfile))]
    private bool _isAllAppsActive = true;

    public bool HasActiveProfile => !IsAllAppsActive;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _isBusy;

    public bool HasProfiles => HasAnyProfiles;
    public bool HasNoProfiles => !HasAnyProfiles;

    public ObservableCollection<ProfileRow> Profiles { get; } = new();

    public event EventHandler? ProfilesChanged;

    public async Task LoadAsync()
    {
        try
        {
            var profiles = await AppServices.Profiles.GetAllAsync();
            var activeId = await AppServices.Profiles.GetActiveProfileIdAsync();

            Profiles.Clear();

            foreach (var profile in profiles)
            {
                Profiles.Add(new ProfileRow(profile, profile.Id == activeId));
            }

            HasAnyProfiles = Profiles.Count > 0;
            IsAllAppsActive = activeId == Profile.AllAppsProfileId;

            var active = Profiles.FirstOrDefault(row => row.IsActive);
            ActiveProfileText = active?.Name ?? "All protected apps";
            ActiveProfileSummary = active is null
                ? "Sob enabled protected app lock hobe."
                : $"{active.AppCount} app lock hobe — baki gulo normal vabe cholbe.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Profiles load failed");
            SetStatus("Profiles load korte parlam na: " + ex.Message, isError: true);
        }
    }

    public async Task CreateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var created = await AppServices.Profiles.CreateAsync(NewProfileName);
            NewProfileName = string.Empty;

            await AppServices.ActivityLog.LogAsync(
                ActivityEventType.Settings,
                ActivityOutcome.Success,
                $"Profile '{created.Name}' created.");

            await LoadAsync();
            SetStatus($"'{created.Name}' profile banano holo. Apps assign koro.", isError: false);
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (ArgumentException ex)
        {
            SetStatus(ex.Message, isError: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Creating profile failed");
            SetStatus("Profile create korte parlam na: " + ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ActivateAsync(ProfileRow row)
    {
        await ActivateProfileAsync(row.Id, row.Name);
    }

    public async Task ActivateAllAppsAsync()
    {
        await ActivateProfileAsync(Profile.AllAppsProfileId, "All protected apps");
    }

    public async Task DeleteAsync(ProfileRow row)
    {
        try
        {
            await AppServices.Profiles.DeleteAsync(row.Id);

            if (AppServices.Protection is not null)
            {
                await AppServices.Protection.RefreshAsync(lockRunningInstances: false);
            }

            await AppServices.ActivityLog.LogAsync(
                ActivityEventType.Settings,
                ActivityOutcome.Success,
                $"Profile '{row.Name}' deleted.");

            await LoadAsync();
            SetStatus($"'{row.Name}' profile delete kora holo.", isError: false);
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Deleting profile failed");
            SetStatus("Delete korte parlam na: " + ex.Message, isError: true);
        }
    }

    public async Task<bool> SaveAppsAsync(int profileId, IReadOnlyCollection<int> appIds)
    {
        try
        {
            await AppServices.Profiles.SetAppsAsync(profileId, appIds);

            if (AppServices.Protection is not null)
            {
                await AppServices.Protection.RefreshAsync(lockRunningInstances: true);
            }

            await LoadAsync();
            SetStatus("Profile er app list save kora holo.", isError: false);
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Saving profile apps failed");
            SetStatus("App list save korte parlam na: " + ex.Message, isError: true);
            return false;
        }
    }

    private async Task ActivateProfileAsync(int profileId, string name)
    {
        try
        {
            await AppServices.Profiles.SetActiveProfileIdAsync(profileId);

            if (AppServices.Protection is not null)
            {
                await AppServices.Protection.RefreshAsync(lockRunningInstances: true);
            }

            await AppServices.ActivityLog.LogAsync(
                ActivityEventType.Settings,
                ActivityOutcome.Success,
                $"Active profile set to '{name}'.");

            await LoadAsync();
            SetStatus($"Active profile: {name}.", isError: false);
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Activating profile failed");
            SetStatus("Profile activate korte parlam na: " + ex.Message, isError: true);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}
