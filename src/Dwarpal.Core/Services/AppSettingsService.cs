using Dwarpal.Core.Data;
using Dwarpal.Core.Models;

namespace Dwarpal.Core.Services;

/// <summary>
/// Settings table er upor typed wrapper. Invalid value ashle default e fallback kore.
/// </summary>
public sealed class AppSettingsService
{
    private readonly SettingsRepository _settings;

    public AppSettingsService(SettingsRepository settings) => _settings = settings;

    public async Task<AutoRelockSettings> GetAutoRelockAsync(CancellationToken cancellationToken = default)
    {
        var minutes = await _settings.GetIntAsync(
            SettingKeys.AutoRelockDefaultMinutes, AutoRelockSettings.DefaultMinutesValue, cancellationToken);
        var grace = await _settings.GetIntAsync(
            SettingKeys.AutoRelockGraceSeconds, AutoRelockSettings.DefaultGraceSeconds, cancellationToken);

        return new AutoRelockSettings(NormalizeMinutes(minutes), Math.Clamp(grace, 0, 300));
    }

    public async Task SaveAutoRelockAsync(AutoRelockSettings settings, CancellationToken cancellationToken = default)
    {
        await _settings.SetIntAsync(
            SettingKeys.AutoRelockDefaultMinutes, NormalizeMinutes(settings.DefaultMinutes), cancellationToken);
        await _settings.SetIntAsync(
            SettingKeys.AutoRelockGraceSeconds, Math.Clamp(settings.GraceSeconds, 0, 300), cancellationToken);
    }

    public Task<bool> GetAutoStartEnabledAsync(CancellationToken cancellationToken = default)
        => _settings.GetBoolAsync(SettingKeys.AutoStartEnabled, false, cancellationToken);

    public Task SetAutoStartEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        => _settings.SetBoolAsync(SettingKeys.AutoStartEnabled, enabled, cancellationToken);

    public Task<bool> GetWatchdogEnabledAsync(CancellationToken cancellationToken = default)
        => _settings.GetBoolAsync(SettingKeys.WatchdogEnabled, false, cancellationToken);

    public Task SetWatchdogEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        => _settings.SetBoolAsync(SettingKeys.WatchdogEnabled, enabled, cancellationToken);

    public Task<bool> GetSelfieEnabledAsync(CancellationToken cancellationToken = default)
        => _settings.GetBoolAsync(SettingKeys.SelfieEnabled, true, cancellationToken);

    public Task SetSelfieEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        => _settings.SetBoolAsync(SettingKeys.SelfieEnabled, enabled, cancellationToken);

    private static int NormalizeMinutes(int minutes)
        => minutes == AutoRelockSettings.Never || minutes is >= 1 and <= 1440
            ? minutes
            : AutoRelockSettings.DefaultMinutesValue;
}
