namespace Dwarpal.Core.Security;

/// <summary>
/// Auto re-lock er pure decision logic (unit testable).
/// App foreground na thakle background timer chalu hoy; timeout + grace sesh hole abar lock.
/// </summary>
public static class AutoRelockPolicy
{
    public static bool ShouldRelock(
        DateTimeOffset now,
        DateTimeOffset unlockedAtUtc,
        DateTimeOffset? backgroundedAtUtc,
        int? timeoutMinutes,
        int graceSeconds)
    {
        if (timeoutMinutes is null or <= 0 || backgroundedAtUtc is null)
        {
            return false;
        }

        if (now < unlockedAtUtc)
        {
            return false;
        }

        // Unlock er por chhoto grace - user dorkar moto app use korte parbe.
        var grace = TimeSpan.FromSeconds(Math.Max(0, graceSeconds));
        if (now - unlockedAtUtc < grace)
        {
            return false;
        }

        return now - backgroundedAtUtc.Value >= TimeSpan.FromMinutes(timeoutMinutes.Value);
    }
}
