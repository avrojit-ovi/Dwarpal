namespace Dwarpal.Core.Models;

/// <summary>
/// Ekta protected application. Matching dui vabe hoy: full path (exact) othoba
/// process name (path unknown hole, ar copy/rename case er jonno).
/// </summary>
public sealed record ProtectedApp(
    int Id,
    string DisplayName,
    string ExecutablePath,
    string ProcessName,
    bool IsEnabled,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? LastLockedUtc,
    int? AutoRelockMinutes = null);
