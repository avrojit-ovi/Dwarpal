using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Dwarpal.App.Services;
using Dwarpal.Core.Data;
using Dwarpal.Core.Models;
using Serilog;

namespace Dwarpal.App.ViewModels;

public sealed record ActivityRow(string TimeText, string Title, string Detail, bool IsFailure);

public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private string _greeting = "Hello";

    [ObservableProperty]
    private string _activeProfile = "All apps";

    [ObservableProperty]
    private int _protectedAppCount;

    [ObservableProperty]
    private int _failedUnlocksToday;

    [ObservableProperty]
    private string _credentialLabel = "—";

    [ObservableProperty]
    private string _recoveryKeyStatus = "Not confirmed";

    [ObservableProperty]
    private string _protectionStatusText = "Starting…";

    [ObservableProperty]
    private bool _isProtectionActive;

    [ObservableProperty]
    private string _autoRelockLabel = "—";

    [ObservableProperty]
    private string _protectedAppsSubtext = "No apps protected yet";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActivityEmpty))]
    private bool _hasRecentActivity;

    public bool IsActivityEmpty => !HasRecentActivity;

    public ObservableCollection<ActivityRow> RecentActivity { get; } = new();

    public async Task LoadAsync()
    {
        Greeting = BuildGreeting();

        try
        {
            var kind = await AppServices.Credentials.GetKindAsync();
            CredentialLabel = kind switch
            {
                CredentialKind.Pin => "PIN (4–8 digits)",
                CredentialKind.Password => "Password",
                _ => "Not configured",
            };

            var acknowledged = await AppServices.Settings.GetAsync(SettingKeys.RecoveryKeyAcknowledgedUtc);
            RecoveryKeyStatus = string.IsNullOrEmpty(acknowledged) ? "Not confirmed" : "Saved";

            var apps = await AppServices.ProtectedApps.GetAllAsync();
            var profiles = await AppServices.Profiles.GetAllAsync();
            var activeProfileId = await AppServices.Profiles.GetActiveProfileIdAsync();

            ActiveProfile = activeProfileId == Profile.AllAppsProfileId
                ? "All apps"
                : profiles.FirstOrDefault(profile => profile.Id == activeProfileId)?.Name ?? "All apps";

            ProtectedAppCount = apps.Count(app => app.IsEnabled);

            var engine = AppServices.Protection;
            IsProtectionActive = engine?.IsRunning == true;
            ProtectionStatusText = engine switch
            {
                null => "Not started",
                { IsRunning: true } => $"Active · {engine.EnabledAppCount} app watching",
                { LastError: not null } => "Watcher error",
                _ => "Starting…",
            };
            ProtectedAppsSubtext = apps.Count == 0
                ? "Add apps to start guarding them"
                : $"{apps.Count(app => app.IsEnabled)} enabled · live process watch";

            var autoRelock = await AppServices.AppSettings.GetAutoRelockAsync();
            AutoRelockLabel = autoRelock.IsGloballyDisabled
                ? "Off"
                : $"After {autoRelock.DefaultMinutes} min idle/background";

            var startOfDayUtc = new DateTimeOffset(DateTime.Today).ToUniversalTime();
            FailedUnlocksToday = await AppServices.Activity.CountAsync(
                ActivityEventType.Unlock, ActivityOutcome.Failure, startOfDayUtc);

            RecentActivity.Clear();
            var entries = await AppServices.Activity.QueryAsync(limit: 6);

            foreach (var entry in entries)
            {
                RecentActivity.Add(new ActivityRow(
                    entry.TimestampUtc.ToLocalTime().ToString("HH:mm"),
                    BuildTitle(entry),
                    entry.Detail ?? string.Empty,
                    entry.Outcome == ActivityOutcome.Failure));
            }

            HasRecentActivity = RecentActivity.Count > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Dashboard load failed");
        }
    }

    private static string BuildGreeting()
    {
        var hour = DateTime.Now.Hour;
        return hour switch
        {
            < 5 => "Still up?",
            < 12 => "Good morning",
            < 17 => "Good afternoon",
            < 22 => "Good evening",
            _ => "Good night",
        };
    }

    private static string BuildTitle(ActivityEntry entry)
    {
        var kind = entry.EventType switch
        {
            ActivityEventType.Setup => "Setup",
            ActivityEventType.Unlock => "Unlock attempt",
            ActivityEventType.Lock => "App lock",
            ActivityEventType.Recovery => "Recovery",
            ActivityEventType.Settings => "Settings",
            ActivityEventType.Tamper => "Tamper alert",
            ActivityEventType.Error => "Error",
            _ => entry.EventType.ToString(),
        };

        var outcome = entry.Outcome switch
        {
            ActivityOutcome.Success => "success",
            ActivityOutcome.Failure => "failed",
            _ => "info",
        };

        return $"{kind} · {outcome}";
    }
}
