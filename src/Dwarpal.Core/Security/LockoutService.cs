using System.Globalization;
using Dwarpal.Core.Data;

namespace Dwarpal.Core.Security;

public sealed record LockoutStatus(int FailedAttempts, int Strikes, int MaxAttempts, int RemainingSeconds)
{
    public bool IsLockedOut => RemainingSeconds > 0;
}

/// <summary>
/// Persistent failed-unlock lockout. Settings table e state rakha hoy, tai app restart korleo
/// cooldown thake. Prottek lockout er por duration double hoy (30s, 60s, 120s ... max 15 min).
/// </summary>
public sealed class LockoutService
{
    public const int DefaultMaxAttempts = 5;
    public const int BaseLockoutSeconds = 30;
    public const int MaxLockoutSeconds = 15 * 60;

    private readonly SettingsRepository _settings;
    private readonly Func<DateTimeOffset> _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LockoutService(SettingsRepository settings, Func<DateTimeOffset>? clock = null)
    {
        _settings = settings;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<LockoutStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            return await ReadStatusAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LockoutStatus> RegisterFailureAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            var current = await ReadStatusAsync(cancellationToken);

            if (current.IsLockedOut)
            {
                return current;
            }

            var failed = current.FailedAttempts + 1;

            if (failed < DefaultMaxAttempts)
            {
                await _settings.SetIntAsync(SettingKeys.LockoutFailedAttempts, failed, cancellationToken);
                return new LockoutStatus(failed, current.Strikes, DefaultMaxAttempts, 0);
            }

            var strikes = current.Strikes + 1;
            var duration = CalculateDurationSeconds(strikes);
            var until = _clock().AddSeconds(duration);

            await _settings.SetIntAsync(SettingKeys.LockoutFailedAttempts, 0, cancellationToken);
            await _settings.SetIntAsync(SettingKeys.LockoutStrikes, strikes, cancellationToken);
            await _settings.SetAsync(
                SettingKeys.LockoutUntilUtc,
                until.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                cancellationToken);

            return new LockoutStatus(0, strikes, DefaultMaxAttempts, duration);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            await _settings.SetIntAsync(SettingKeys.LockoutFailedAttempts, 0, cancellationToken);
            await _settings.SetIntAsync(SettingKeys.LockoutStrikes, 0, cancellationToken);
            await _settings.SetAsync(SettingKeys.LockoutUntilUtc, string.Empty, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public static int CalculateDurationSeconds(int strikes)
    {
        if (strikes <= 0)
        {
            return 0;
        }

        var exponent = Math.Min(strikes - 1, 10);
        var seconds = BaseLockoutSeconds * (1L << exponent);
        return (int)Math.Min(seconds, MaxLockoutSeconds);
    }

    private async Task<LockoutStatus> ReadStatusAsync(CancellationToken cancellationToken)
    {
        var failed = await _settings.GetIntAsync(SettingKeys.LockoutFailedAttempts, 0, cancellationToken);
        var strikes = await _settings.GetIntAsync(SettingKeys.LockoutStrikes, 0, cancellationToken);
        var untilText = await _settings.GetAsync(SettingKeys.LockoutUntilUtc, cancellationToken);

        if (string.IsNullOrWhiteSpace(untilText)
            || !DateTimeOffset.TryParse(
                untilText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var until))
        {
            return new LockoutStatus(failed, strikes, DefaultMaxAttempts, 0);
        }

        var remaining = until - _clock();
        if (remaining <= TimeSpan.Zero)
        {
            // Cooldown sesh - strikes rekhe dei, jate porer lockout beshi somoy hoy.
            await _settings.SetAsync(SettingKeys.LockoutUntilUtc, string.Empty, cancellationToken);
            return new LockoutStatus(failed, strikes, DefaultMaxAttempts, 0);
        }

        return new LockoutStatus(
            failed,
            strikes,
            DefaultMaxAttempts,
            (int)Math.Ceiling(remaining.TotalSeconds));
    }
}
