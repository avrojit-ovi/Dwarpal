namespace Dwarpal.Core.Models;

/// <summary>
/// Auto re-lock configuration. DefaultMinutes = -1 mane auto re-lock off.
/// Per-app override null hole global default use hoy; -1 hole oi app er jonno off.
/// </summary>
public sealed record AutoRelockSettings(int DefaultMinutes, int GraceSeconds)
{
    public const int Never = -1;

    public static AutoRelockSettings Default { get; } = new(DefaultMinutesValue, DefaultGraceSeconds);

    public const int DefaultMinutesValue = 5;
    public const int DefaultGraceSeconds = 5;

    public bool IsGloballyDisabled => DefaultMinutes == Never;

    /// <summary>Null return mane ei app er jonno auto re-lock nei.</summary>
    public int? ResolveTimeout(ProtectedApp app)
    {
        var minutes = app.AutoRelockMinutes ?? DefaultMinutes;
        return minutes == Never || minutes <= 0 ? null : minutes;
    }
}
