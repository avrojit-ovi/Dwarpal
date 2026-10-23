using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dwarpal.App.Services;
using Dwarpal.Core.Data;
using Dwarpal.Core.Models;
using Dwarpal.Core.Services;
using Serilog;

namespace Dwarpal.App.ViewModels;

/// <summary>
/// Settings page: auto re-lock timeout, unlock grace period, auto-start.
/// Sob change sathe sathe save hoy - alada "Apply" button er dorkar nei.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private static readonly int[] MinuteSteps = { AutoRelockSettings.Never, 1, 2, 5, 10, 15, 30, 60 };
    private static readonly int[] GraceSteps = { 0, 5, 10, 15, 30, 60 };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutoRelockText), nameof(AutoRelockHint))]
    private int _autoRelockMinutes = AutoRelockSettings.DefaultMinutesValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GraceText))]
    private int _graceSeconds = AutoRelockSettings.DefaultGraceSeconds;

    [ObservableProperty]
    private bool _autoStartEnabled;

    [ObservableProperty]
    private bool _watchdogEnabled;

    [ObservableProperty]
    private bool _selfieEnabled = true;

    [ObservableProperty]
    private string _hardeningLabel = "—";

    [ObservableProperty]
    private string _panicHotkeyStatus = "—";

    [ObservableProperty]
    private bool _panicHotkeyActive;

    [ObservableProperty]
    private bool _hardeningActive;

    [ObservableProperty]
    private string _credentialLabel = "—";

    [ObservableProperty]
    private string _recoveryKeyStatus = "Not confirmed";

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    public string VersionText =>
        $"Dwarpal {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    public string AutoRelockText => AutoRelockMinutes == AutoRelockSettings.Never
        ? "Never"
        : $"{AutoRelockMinutes} minute{(AutoRelockMinutes == 1 ? string.Empty : "s")}";

    public string AutoRelockHint => AutoRelockMinutes == AutoRelockSettings.Never
        ? "Unlock korar por app ta ar re-lock hobe na."
        : $"App background e {AutoRelockText} thakle abar lock screen ashbe.";

    public string GraceText => GraceSeconds == 0
        ? "No grace"
        : $"{GraceSeconds} seconds";

    public async Task LoadAsync()
    {
        try
        {
            var settings = await AppServices.AppSettings.GetAutoRelockAsync();
            AutoRelockMinutes = settings.DefaultMinutes;
            GraceSeconds = settings.GraceSeconds;

            AutoStartEnabled = await AppServices.AppSettings.GetAutoStartEnabledAsync();

            // Task Scheduler e task thakle setai asol source of truth.
            var scheduled = AutoStartService.IsEnabled();
            if (AutoStartEnabled != scheduled)
            {
                AutoStartEnabled = scheduled;
                await AppServices.AppSettings.SetAutoStartEnabledAsync(scheduled);
            }

            var kind = await AppServices.Credentials.GetKindAsync();
            CredentialLabel = kind switch
            {
                CredentialKind.Pin => "PIN (4–8 digits)",
                CredentialKind.Password => "Password",
                _ => "Not configured",
            };

            // Watchdog Task Scheduler ei asol source of truth.
            var watchdog = WatchdogService.IsEnabled();
            WatchdogEnabled = watchdog;

            if (await AppServices.AppSettings.GetWatchdogEnabledAsync() != watchdog)
            {
                await AppServices.AppSettings.SetWatchdogEnabledAsync(watchdog);
            }

            SelfieEnabled = await AppServices.AppSettings.GetSelfieEnabledAsync();

            PanicHotkeyActive = AppServices.PanicHotkey.IsRegistered;
            PanicHotkeyStatus = PanicHotkeyActive
                ? "Active"
                : AppServices.PanicHotkey.Error is { Length: > 0 } hotkeyError
                    ? "Off · " + hotkeyError
                    : "Off";

            HardeningActive = AppServices.ProcessHardeningActive;
            HardeningLabel = HardeningActive
                ? "Active · deny terminate to Users"
                : AppServices.ProcessHardeningError is { Length: > 0 } error
                    ? "Off · " + error
                    : "Off";

            var acknowledged = await AppServices.Settings.GetAsync(SettingKeys.RecoveryKeyAcknowledgedUtc);
            RecoveryKeyStatus = string.IsNullOrEmpty(acknowledged) ? "Not confirmed" : "Saved";

            StatusMessage = null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Settings load failed");
            SetStatus("Settings load korte parlam na: " + ex.Message, isError: true);
        }
    }

    [RelayCommand]
    private async Task PreviousAutoRelockAsync() => await StepAutoRelockAsync(-1);

    [RelayCommand]
    private async Task NextAutoRelockAsync() => await StepAutoRelockAsync(+1);

    [RelayCommand]
    private async Task PreviousGraceAsync() => await StepGraceAsync(-1);

    [RelayCommand]
    private async Task NextGraceAsync() => await StepGraceAsync(+1);

    public async Task SetAutoStartAsync(bool enabled)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = enabled ? AutoStartService.Enable() : AutoStartService.Disable();

            if (!result.Success)
            {
                AutoStartEnabled = !enabled;
                SetStatus(
                    $"Auto-start {(enabled ? "on" : "off")} korte parlam na. {result.Message}",
                    isError: true);
                return;
            }

            await AppServices.AppSettings.SetAutoStartEnabledAsync(enabled);
            AutoStartEnabled = enabled;

            await AppServices.ActivityLog.LogAsync(
                ActivityEventType.Settings,
                ActivityOutcome.Success,
                $"Auto-start {(enabled ? "enabled" : "disabled")} via Task Scheduler.");

            SetStatus(
                enabled
                    ? "Dwarpal ekhon sign-in e nijei start hobe."
                    : "Sign-in auto-start bondho kora holo.",
                isError: false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Auto-start toggle failed");
            AutoStartEnabled = !enabled;
            SetStatus("Auto-start update korte parlam na: " + ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SetWatchdogAsync(bool enabled)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = enabled ? WatchdogService.Enable() : WatchdogService.Disable();

            if (!result.Success)
            {
                WatchdogEnabled = !enabled;
                SetStatus(
                    $"Watchdog {(enabled ? "on" : "off")} korte parlam na. {result.Message}",
                    isError: true);
                return;
            }

            await AppServices.AppSettings.SetWatchdogEnabledAsync(enabled);
            WatchdogEnabled = enabled;

            await AppServices.ActivityLog.LogAsync(
                ActivityEventType.Tamper,
                ActivityOutcome.Success,
                $"Watchdog {(enabled ? "enabled" : "disabled")} (every 1 minute).");

            SetStatus(
                enabled
                    ? "Watchdog on: Dwarpal bondho hole 1 minute er moddhe abar start hobe."
                    : "Watchdog off kora holo.",
                isError: false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Watchdog toggle failed");
            WatchdogEnabled = !enabled;
            SetStatus("Watchdog update korte parlam na: " + ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SetSelfieAsync(bool enabled)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            await AppServices.AppSettings.SetSelfieEnabledAsync(enabled);
            SelfieEnabled = enabled;

            await AppServices.ActivityLog.LogAsync(
                ActivityEventType.Settings,
                ActivityOutcome.Success,
                $"Intruder selfie {(enabled ? "enabled" : "disabled")}.");

            SetStatus(
                enabled
                    ? "Bar bar fail hole webcam theke encrypted snapshot newa hobe."
                    : "Intruder selfie bondho kora holo.",
                isError: false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Selfie toggle failed");
            SelfieEnabled = !enabled;
            SetStatus("Selfie setting save korte parlam na: " + ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StepAutoRelockAsync(int direction)
    {
        var index = Array.IndexOf(MinuteSteps, AutoRelockMinutes);
        index = index < 0 ? Array.IndexOf(MinuteSteps, AutoRelockSettings.DefaultMinutesValue) : index;
        index = Math.Clamp(index + direction, 0, MinuteSteps.Length - 1);

        if (MinuteSteps[index] == AutoRelockMinutes)
        {
            return;
        }

        AutoRelockMinutes = MinuteSteps[index];
        await SaveAutoRelockAsync();
    }

    private async Task StepGraceAsync(int direction)
    {
        var index = Array.IndexOf(GraceSteps, GraceSeconds);
        index = index < 0 ? Array.IndexOf(GraceSteps, AutoRelockSettings.DefaultGraceSeconds) : index;
        index = Math.Clamp(index + direction, 0, GraceSteps.Length - 1);

        if (GraceSteps[index] == GraceSeconds)
        {
            return;
        }

        GraceSeconds = GraceSteps[index];
        await SaveAutoRelockAsync();
    }

    private async Task SaveAutoRelockAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            await AppServices.AppSettings.SaveAutoRelockAsync(
                new AutoRelockSettings(AutoRelockMinutes, GraceSeconds));

            if (AppServices.Protection is not null)
            {
                await AppServices.Protection.ReloadAutoRelockSettingsAsync();
            }

            await AppServices.ActivityLog.LogAsync(
                ActivityEventType.Settings,
                ActivityOutcome.Success,
                AutoRelockMinutes == AutoRelockSettings.Never
                    ? "Auto re-lock disabled."
                    : $"Auto re-lock set to {AutoRelockMinutes} min (grace {GraceSeconds}s).");

            SetStatus("Auto re-lock settings saved.", isError: false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Saving auto re-lock settings failed");
            SetStatus("Save korte parlam na: " + ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}
