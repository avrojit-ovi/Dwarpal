using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Dwarpal.App.Services;
using Dwarpal.Core.Models;
using Serilog;

namespace Dwarpal.App.ViewModels;

public sealed class ProtectedAppRow
{
    public ProtectedAppRow(ProtectedApp app, ImageSource? icon)
    {
        Id = app.Id;
        DisplayName = app.DisplayName;
        ExecutablePath = app.ExecutablePath;
        ProcessName = app.ProcessName;
        IsEnabled = app.IsEnabled;
        Icon = icon;
        LastLockedText = app.LastLockedUtc is null
            ? "Never locked yet"
            : $"Last locked {app.LastLockedUtc.Value.ToLocalTime():dd MMM, HH:mm}";
    }

    public int Id { get; }
    public string DisplayName { get; }
    public string ExecutablePath { get; }
    public string ProcessName { get; }
    public bool IsEnabled { get; }
    public ImageSource? Icon { get; }
    public string LastLockedText { get; }
    public string Initial => string.IsNullOrEmpty(DisplayName)
        ? "?"
        : DisplayName.Trim()[0].ToString().ToUpperInvariant();
}

public partial class ProtectedAppsViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoApps))]
    private bool _hasApps;

    public bool HasNoApps => !HasApps;

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isStatusError;

    public ObservableCollection<ProtectedAppRow> Apps { get; } = new();

    public event EventHandler? AppsChanged;

    public async Task LoadAsync()
    {
        IsLoading = true;

        try
        {
            var apps = await AppServices.ProtectedApps.GetAllAsync();

            Apps.Clear();
            foreach (var app in apps)
            {
                Apps.Add(new ProtectedAppRow(app, IconLoader.Load(app.ExecutablePath, 32)));
            }

            Count = Apps.Count;
            HasApps = Count > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Protected apps load failed");
            SetStatus("Protected apps load korte parlam na: " + ex.Message, isError: true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<bool> AddAppAsync(string displayName, string executablePath)
    {
        try
        {
            if (await AppServices.ProtectedApps.ExistsAsync(executablePath))
            {
                SetStatus("Ei app ta already protected list e ache.", isError: true);
                return false;
            }

            var added = await AppServices.ProtectedApps.AddAsync(displayName, executablePath);

            // Already chalu thakle sathe sathe lock hobe (spec requirement).
            if (AppServices.Protection is not null)
            {
                try
                {
                    await AppServices.Protection.RefreshAsync(lockRunningInstances: true);
                }
                catch (Exception ex)
                {
                    // App add hoye geche - lock refresh er error ke "add fail" dekhabe na.
                    Log.Warning(ex, "Post-add lock refresh failed");
                }
            }

            await LoadAsync();
            SetStatus($"{added.DisplayName} ekhon protected.", isError: false);
            AppsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Adding protected app failed");
            SetStatus("App add korte parlam na: " + ex.Message, isError: true);
            return false;
        }
    }

    public async Task SetEnabledAsync(ProtectedAppRow row, bool enabled)
    {
        try
        {
            await AppServices.ProtectedApps.SetEnabledAsync(row.Id, enabled);

            if (AppServices.Protection is not null)
            {
                await AppServices.Protection.RefreshAsync(lockRunningInstances: false);
            }

            await LoadAsync();
            SetStatus($"{row.DisplayName} {(enabled ? "enabled" : "paused")}.", isError: false);
            AppsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Toggling protected app failed");
            SetStatus("Update korte parlam na: " + ex.Message, isError: true);
            await LoadAsync();
        }
    }

    public async Task RemoveAsync(ProtectedAppRow row)
    {
        try
        {
            await AppServices.ProtectedApps.RemoveAsync(row.Id);

            if (AppServices.Protection is not null)
            {
                await AppServices.Protection.RefreshAsync(lockRunningInstances: false);
            }

            await LoadAsync();
            SetStatus($"{row.DisplayName} ar protect hocche na.", isError: false);
            AppsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Removing protected app failed");
            SetStatus("Remove korte parlam na: " + ex.Message, isError: true);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}
